using ChronicleOfHeros.Identity.AspNetCore.Application;
using ChronicleOfHeros.Identity.Contracts;

using Microsoft.AspNetCore.Identity;

namespace ChronicleOfHeros.Identity.AspNetCore.Infrastructure.Identity;

// This class gets used by the dependency injection system 
// and may not be directly instantiated.
#pragma warning disable CA1812 // Avoid uninstantiated internal classes

internal sealed class AuthenticationService(
    UserManager<ApplicationUser> userManager,
    AuthenticationTokenService tokenService) : IAuthenticationService
{
    public async Task<IdentityOperationResult<AccessTokenResponse>> SignInAsync(
        SignInRequest request,
        CancellationToken cancellationToken)
    {
        string? username = request.Username?.Trim();
        ApplicationUser? user = string.IsNullOrWhiteSpace(username)
            ? null
            : await userManager.FindByNameAsync(username).ConfigureAwait(false);
        if (user is null || !user.IsActive || !await userManager.CheckPasswordAsync(user, request.Password ?? string.Empty).ConfigureAwait(false))
        {
            return IdentityOperationResults.Unauthorized<AccessTokenResponse>();
        }

        if (user.MustChangePassword)
        {
            return IdentityOperationResults.Success<AccessTokenResponse>(
                tokenService.CreateRestrictedAccessToken(user));
        }

        IList<string> roles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
        return IdentityOperationResults.Success<AccessTokenResponse>(
            await tokenService.CreateNormalTokenPairAsync(user, roles, cancellationToken).ConfigureAwait(false));
    }

    public async Task<IdentityOperationResult<TokenPairResponse>> ChangePasswordAsync(
        Guid accountId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(accountId.ToString()).ConfigureAwait(false);
        if (user is null || !user.IsActive || !await userManager.CheckPasswordAsync(user, request.CurrentPassword ?? string.Empty).ConfigureAwait(false))
        {
            return IdentityOperationResults.Unauthorized<TokenPairResponse>();
        }

        IdentityResult passwordChange = await userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword ?? string.Empty,
            request.NewPassword ?? string.Empty).ConfigureAwait(false);
        if (!passwordChange.Succeeded)
        {
            return passwordChange.ToValidationResult<TokenPairResponse>();
        }

        user.MustChangePassword = false;
        IdentityResult userUpdate = await userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!userUpdate.Succeeded)
        {
            return userUpdate.ToValidationResult<TokenPairResponse>();
        }

        await tokenService.RevokeAllRefreshSessionsAsync(user.Id, cancellationToken).ConfigureAwait(false);
        IList<string> roles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
        return IdentityOperationResults.Success(
            await tokenService.CreateNormalTokenPairAsync(user, roles, cancellationToken).ConfigureAwait(false));
    }

    public async Task<IdentityOperationResult<TokenPairResponse>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        TokenPairResponse? tokenPair = await tokenService.RefreshNormalTokenPairAsync(
            request.RefreshToken,
            userManager,
            cancellationToken).ConfigureAwait(false);

        return tokenPair is null
            ? IdentityOperationResults.Unauthorized<TokenPairResponse>()
            : IdentityOperationResults.Success(tokenPair);
    }

    public Task SignOutAsync(RefreshTokenRequest request, CancellationToken cancellationToken) =>
        tokenService.RevokeRefreshSessionFamilyForTokenAsync(request.RefreshToken, cancellationToken);
}

#pragma warning restore CA1812 // Avoid uninstantiated internal classes
