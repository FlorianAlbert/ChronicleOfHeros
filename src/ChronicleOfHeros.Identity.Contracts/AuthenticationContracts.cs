namespace ChronicleOfHeros.Identity.Contracts;

/// <summary>
/// Represents a service for handling authentication operations such as signing in, changing passwords, refreshing tokens, and signing out.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Signs in a user with the provided credentials and returns an access token response.
    /// </summary>
    /// <param name="request">The sign-in request containing the user's credentials.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the sign-in operation, including an access token response if successful.</returns>
    Task<IdentityOperationResult<AccessTokenResponse>> SignInAsync(
        SignInRequest request,
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Changes the password for the specified account and returns a token pair response.
    /// </summary>
    /// <param name="accountId">The ID of the account for which to change the password.</param>
    /// <param name="request">The change password request containing the current and new passwords.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the change password operation, including a token pair response if successful.</returns>
    Task<IdentityOperationResult<TokenPairResponse>> ChangePasswordAsync(
        Guid accountId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Refreshes the access token using the provided refresh token and returns a new token pair response.
    /// </summary>
    /// <param name="request">The refresh token request containing the refresh token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the refresh operation, including a new token pair response if successful.</returns>
    Task<IdentityOperationResult<TokenPairResponse>> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Signs out the user by invalidating the provided refresh token.
    /// </summary>
    /// <param name="request">The refresh token request containing the refresh token to invalidate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous sign-out operation.</returns>
    Task SignOutAsync(RefreshTokenRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Represents a request to sign in a user with their username and password.
/// </summary>
/// <param name="Username">The username of the user.</param>
/// <param name="Password">The password of the user.</param>
public sealed record SignInRequest(string? Username, string? Password);

/// <summary>
/// Represents a request to change the password for a user account, including the current and new passwords.
/// </summary>
/// <param name="CurrentPassword">The current password of the user.</param>
/// <param name="NewPassword">The new password of the user.</param>
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

/// <summary>
/// Represents a request to refresh an access token using a refresh token.
/// </summary>
/// <param name="RefreshToken">The refresh token used to obtain a new access token.</param>
public sealed record RefreshTokenRequest(string? RefreshToken);

/// <summary>
/// Represents the response containing an access token and its expiration time.
/// </summary>
/// <param name="AccessToken">The access token.</param>
/// <param name="AccessTokenExpiresAt">The expiration time of the access token.</param>
public abstract record AccessTokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt);

/// <summary>
/// Represents the response containing a restricted access token and its expiration time.
/// This type of token has limited permissions or scope compared to a full access token.
/// For example, it may only be used to change the password of a user account, and cannot be used for other operations.
/// </summary>
/// <param name="AccessToken">The restricted access token.</param>
/// <param name="AccessTokenExpiresAt">The expiration time of the restricted access token.</param>
public sealed record RestrictedAccessTokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt)
    : AccessTokenResponse(AccessToken, AccessTokenExpiresAt);

/// <summary>
/// Represents the response containing a pair of tokens: an access token and a refresh token, along with their respective expiration times.
/// </summary>
/// <param name="AccessToken">The access token.</param>
/// <param name="AccessTokenExpiresAt">The expiration time of the access token.</param>
/// <param name="RefreshToken">The refresh token.</param>
/// <param name="RefreshTokenExpiresAt">The expiration time of the refresh token.</param>
public sealed record TokenPairResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt)
    : AccessTokenResponse(AccessToken, AccessTokenExpiresAt);