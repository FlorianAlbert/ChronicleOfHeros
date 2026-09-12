namespace ChronicleOfHeros.Identity.Contracts;

/// <summary>
/// Represents a service for retrieving Player identity information.
/// </summary>
public interface IPlayerIdentityService
{
    /// <summary>
    /// Retrieves the identity information for the specified Player account.
    /// </summary>
    /// <param name="accountId">The unique identifier of the Player account.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the Player identity operation.</returns>
    Task<IdentityOperationResult<PlayerIdentityResponse>> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Represents the response for a player's identity, containing the unique account identifier.
/// </summary>
/// <param name="AccountId">The unique identifier of the player's account.</param>
public sealed record PlayerIdentityResponse(Guid AccountId);