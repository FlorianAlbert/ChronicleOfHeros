using ChronicleOfHeros.Identity.AspNetCore.Application;
using ChronicleOfHeros.Identity.Contracts;

using Microsoft.AspNetCore.Identity;

namespace ChronicleOfHeros.Identity.AspNetCore.Infrastructure.Identity;

internal static class IdentityResultExtensions
{
    extension(IdentityResult result)
    {
        internal IdentityOperationResult<T> ToValidationResult<T>() =>
            IdentityOperationResults.Validation<T>(result.Errors
                .GroupBy(error => error.Code)
                .ToDictionary(
                    errors => errors.Key,
                    errors => (IReadOnlyList<string>)[.. errors.Select(error => error.Description)]));
    }
}