using ChronicleOfHeros.Identity.Contracts;

namespace ChronicleOfHeros.Identity.AspNetCore.Application;

internal static class IdentityOperationResults
{
    internal static IdentityOperationResult<T> Success<T>(T value) => new(value, null);

    internal static IdentityOperationResult<T> Unauthorized<T>() =>
        new(default, new IdentityFailure(IdentityFailureKind.Unauthorized));

    internal static IdentityOperationResult<T> NotFound<T>() =>
        new(default, new IdentityFailure(IdentityFailureKind.NotFound));

    internal static IdentityOperationResult<T> Validation<T>(
        IReadOnlyDictionary<string, IReadOnlyList<string>> errors) =>
        new(default, new IdentityFailure(IdentityFailureKind.Validation, errors));
}