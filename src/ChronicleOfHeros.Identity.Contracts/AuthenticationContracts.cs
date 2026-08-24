namespace ChronicleOfHeros.Identity.Contracts;

public interface IAuthenticationService
{
    Task<IdentityOperationResult<AccessTokenResponse>> SignInAsync(
        SignInRequest request,
        CancellationToken cancellationToken);

    Task<IdentityOperationResult<TokenPairResponse>> ChangePasswordAsync(
        Guid accountId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken);

    Task<IdentityOperationResult<TokenPairResponse>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken);

    Task SignOutAsync(RefreshTokenRequest request, CancellationToken cancellationToken);
}

public sealed record SignInRequest(string? Username, string? Password);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record RefreshTokenRequest(string? RefreshToken);

public abstract record AccessTokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt);

public sealed record RestrictedAccessTokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt)
    : AccessTokenResponse(AccessToken, AccessTokenExpiresAt);

public sealed record TokenPairResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt)
    : AccessTokenResponse(AccessToken, AccessTokenExpiresAt);