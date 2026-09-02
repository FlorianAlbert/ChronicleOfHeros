using ChronicleOfHeros.Identity.Contracts;

using Microsoft.AspNetCore.Identity;

namespace ChronicleOfHeros.Identity.AspNetCore.Identity;

internal static class IdentityOperationResults
{
    internal static IdentityOperationResult<T> Success<T>(T value) => new(value, null);

    internal static IdentityOperationResult<T> Unauthorized<T>() =>
        new(default, new IdentityFailure(IdentityFailureKind.Unauthorized));

    internal static IdentityOperationResult<T> NotFound<T>() =>
        new(default, new IdentityFailure(IdentityFailureKind.NotFound));

    internal static IdentityOperationResult<T> Validation<T>(IdentityResult result) =>
        Validation<T>(result.Errors
            .GroupBy(error => error.Code)
            .ToDictionary(
                errors => errors.Key,
                errors => (IReadOnlyList<string>)[.. errors.Select(error => error.Description)]));

    internal static IdentityOperationResult<T> Validation<T>(
        IReadOnlyDictionary<string, IReadOnlyList<string>> errors) =>
        new(default, new IdentityFailure(IdentityFailureKind.Validation, errors));
}