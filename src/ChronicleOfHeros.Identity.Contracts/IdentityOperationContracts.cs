namespace ChronicleOfHeros.Identity.Contracts;

public sealed record IdentityOperationResult<T>(T? Value, IdentityFailure? Failure);

public sealed record IdentityFailure(
    IdentityFailureKind Kind,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? ValidationErrors = null);

public enum IdentityFailureKind
{
    Unauthorized,
    NotFound,
    Validation,
}