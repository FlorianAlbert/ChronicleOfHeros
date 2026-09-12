namespace ChronicleOfHeros.Identity.Contracts;

/// <summary>
/// Represents a service for player administration operations, such as enrolling players and resetting passwords.
/// </summary>
public interface IPlayerAdministrationService
{
    /// <summary>
    /// Enrolls a new player with the specified request details.
    /// </summary>
    /// <param name="request">The request containing the details of the player to enroll.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the player enrollment operation.</returns>
    Task<IdentityOperationResult<PlayerEnrollmentResponse>> EnrollAsync(
        EnrollPlayerRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resets the password for a player with the specified request details.
    /// </summary>
    /// <param name="request">The request containing the details of the password reset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the password reset operation.</returns>
    Task<IdentityOperationResult<TemporaryCredentialResponse>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Represents a request to enroll a new player with the specified username.
/// </summary>
/// <param name="Username">The username of the player to enroll.</param>
public sealed record EnrollPlayerRequest(string? Username);

/// <summary>
/// Represents a request to reset the password for a player with the specified username.
/// </summary>
/// <param name="Username">The username of the player whose password is to be reset.</param>
public sealed record ResetPasswordRequest(string Username);

/// <summary>
/// Represents a response containing a temporary credential for a player.
/// </summary>
/// <param name="TemporaryCredential">The temporary credential for the player.</param>
public sealed record TemporaryCredentialResponse(string TemporaryCredential);

/// <summary>
/// Represents a response containing the account ID and temporary credential for a newly enrolled player.
/// </summary>
/// <param name="AccountId">The unique identifier of the newly enrolled player's account.</param>
/// <param name="TemporaryCredential">The temporary credential for the newly enrolled player.</param>
public sealed record PlayerEnrollmentResponse(
    Guid AccountId,
    TemporaryCredentialResponse TemporaryCredential);