using System.Security.Cryptography;

using ChronicleOfHeros.Identity.Contracts;

using Microsoft.AspNetCore.Identity;

namespace ChronicleOfHeros.Identity.AspNetCore.Identity;

// This class gets used by the dependency injection system 
// and may not be directly instantiated.
#pragma warning disable CA1812 // Avoid uninstantiated internal classes

internal sealed class PlayerAdministrationService(
    UserManager<ApplicationUser> userManager,
    AuthenticationTokenService tokenService) : IPlayerAdministrationService
{
    public async Task<IdentityOperationResult<PlayerEnrollmentResponse>> EnrollAsync(
        EnrollPlayerRequest request,
        CancellationToken cancellationToken)
    {
        string? username = request.Username?.Trim();
        if (!UsernameValidator.IsValid(username))
        {
            return IdentityOperationResults.Validation<PlayerEnrollmentResponse>(
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["username"] = ["The username is invalid."],
                });
        }

        string temporaryCredential = CreateTemporaryCredential();
        ApplicationUser user = new() { UserName = username };
        IdentityResult creation = await userManager.CreateAsync(user, temporaryCredential).ConfigureAwait(false);
        if (!creation.Succeeded)
        {
            return IdentityOperationResults.Validation<PlayerEnrollmentResponse>(creation);
        }

        IdentityResult roleAssignment = await userManager.AddToRoleAsync(user, ApplicationRoles.Player).ConfigureAwait(false);
        if (!roleAssignment.Succeeded)
        {
            _ = await userManager.DeleteAsync(user).ConfigureAwait(false);
            return IdentityOperationResults.Validation<PlayerEnrollmentResponse>(roleAssignment);
        }

        return IdentityOperationResults.Success(new PlayerEnrollmentResponse(
            Guid.Parse(user.Id),
            new TemporaryCredentialResponse(temporaryCredential)));
    }

    public async Task<IdentityOperationResult<TemporaryCredentialResponse>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByNameAsync(request.Username.Trim()).ConfigureAwait(false);
        if (user is null)
        {
            return IdentityOperationResults.NotFound<TemporaryCredentialResponse>();
        }

        string temporaryCredential = CreateTemporaryCredential();
        user.MustChangePassword = true;
        IdentityResult passwordReset = await userManager.ResetPasswordAsync(
            user,
            await userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false),
            temporaryCredential).ConfigureAwait(false);
        if (!passwordReset.Succeeded)
        {
            return IdentityOperationResults.Validation<TemporaryCredentialResponse>(passwordReset);
        }

        await tokenService.RevokeAllRefreshSessionsAsync(user.Id, cancellationToken).ConfigureAwait(false);
        return IdentityOperationResults.Success(new TemporaryCredentialResponse(temporaryCredential));
    }

    private static string CreateTemporaryCredential() =>
        $"Aa{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToUpperInvariant()}!1";
}

#pragma warning restore CA1812 // Avoid uninstantiated internal classes
