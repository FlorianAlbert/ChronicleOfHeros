namespace ChronicleOfHeros.Identity.Contracts;

/// <summary>
/// Represents the response for a player's identity, containing the unique account identifier.
/// </summary>
/// <param name="AccountId">The unique identifier of the player's account.</param>
public sealed record PlayerIdentityResponse(Guid AccountId);