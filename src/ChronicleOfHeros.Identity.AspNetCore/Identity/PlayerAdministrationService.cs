using ChronicleOfHeros.Identity.Contracts;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;

namespace ChronicleOfHeros.Identity.AspNetCore.Identity;

internal sealed class PlayerAdministrationService(
    UserManager<ApplicationUser> userManager,
    AuthenticationTokenService tokenService) : IPlayerAdministrationService
{
    public async Task<IdentityOperationResult<PlayerEnrollmentResponse>> EnrollAsync(
        EnrollPlayerRequest request,
        CancellationToken cancellationToken)
    {
        var username = request.Username?.Trim();
        if (!UsernameValidator.IsValid(username))
        {
            return IdentityOperationResults.Validation<PlayerEnrollmentResponse>(
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["username"] = ["The username is invalid."],
                });
        }

        var temporaryCredential = CreateTemporaryCredential();
        var user = new ApplicationUser { UserName = username };
        var creation = await userManager.CreateAsync(user, temporaryCredential).ConfigureAwait(false);
        if (!creation.Succeeded)
        {
            return IdentityOperationResults.Validation<PlayerEnrollmentResponse>(creation);
        }

        var roleAssignment = await userManager.AddToRoleAsync(user, ApplicationRoles.Player).ConfigureAwait(false);
        if (!roleAssignment.Succeeded)
        {
            await userManager.DeleteAsync(user).ConfigureAwait(false);
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
        var user = await userManager.FindByNameAsync(request.Username.Trim()).ConfigureAwait(false);
        if (user is null)
        {
            return IdentityOperationResults.NotFound<TemporaryCredentialResponse>();
        }

        var temporaryCredential = CreateTemporaryCredential();
        user.MustChangePassword = true;
        var passwordReset = await userManager.ResetPasswordAsync(
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
        $"A{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToUpperInvariant()}!1";
}