namespace ChronicleOfHeros.Api.Identity;

public sealed class RefreshSession
{
    public Guid Id { get; set; }

    public required string UserId { get; set; }

    public required string TokenHash { get; set; }

    public Guid FamilyId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public ApplicationUser User { get; set; } = null!;
}