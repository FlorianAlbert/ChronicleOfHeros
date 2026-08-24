using ChronicleOfHeros.Identity.Contracts;

namespace ChronicleOfHeros.Api.Authentication;

internal static class IdentityHttpResults
{
    internal static IResult From<T>(IdentityOperationResult<T> result, Func<T, IResult> success) =>
        result.Failure?.Kind switch
        {
            null when result.Value is not null => success(result.Value),
            IdentityFailureKind.Unauthorized => Results.Unauthorized(),
            IdentityFailureKind.NotFound => Results.NotFound(),
            IdentityFailureKind.Validation => Results.ValidationProblem(
                result.Failure.ValidationErrors!.ToDictionary(
                    error => error.Key,
                    error => error.Value.ToArray())),
            _ => Results.Problem(),
        };
}