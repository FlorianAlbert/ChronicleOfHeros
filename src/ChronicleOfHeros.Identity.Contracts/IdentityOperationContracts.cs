namespace ChronicleOfHeros.Identity.Contracts;

/// <summary>
/// Represents the result of an identity operation, which can either be a successful result with a value of type T or a failure with an IdentityFailure.
/// </summary>
/// <typeparam name="T">The type of the value returned in case of a successful operation.</typeparam>
/// <param name="Value">The value of the operation if it was successful; otherwise, null.</param>
/// <param name="Failure">The failure details if the operation was not successful; otherwise, null.</param>
public sealed record IdentityOperationResult<T>(T? Value, IdentityFailure? Failure);

/// <summary>
/// Represents the details of a failure that occurred during an identity operation, including the kind of failure and any validation errors that may have occurred.
/// </summary>
/// <param name="Kind">The kind of failure that occurred.</param>
/// <param name="ValidationErrors">The validation errors that occurred, if any; otherwise, null.</param>
public sealed record IdentityFailure(
    IdentityFailureKind Kind,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? ValidationErrors = null);

/// <summary>
/// Represents the different kinds of failures that can occur during an identity operation.
/// </summary>
public enum IdentityFailureKind
{
    /// <summary>
    /// Indicates that the operation failed due to an unauthorized access attempt.
    /// </summary>
    Unauthorized,

    /// <summary>
    /// Indicates that the operation failed because the requested resource was not found.
    /// </summary>
    NotFound,

    /// <summary>
    /// Indicates that the operation failed due to validation errors, such as invalid input data.
    /// </summary>
    Validation,
}