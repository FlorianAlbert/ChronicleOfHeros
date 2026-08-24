namespace ChronicleOfHeros.Identity.Contracts;

public interface IPlayerAdministrationService
{
    Task<IdentityOperationResult<PlayerEnrollmentResponse>> EnrollAsync(
        EnrollPlayerRequest request,
        CancellationToken cancellationToken);

    Task<IdentityOperationResult<TemporaryCredentialResponse>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken);
}

public sealed record EnrollPlayerRequest(string? Username);

public sealed record ResetPasswordRequest(string Username);

public sealed record TemporaryCredentialResponse(string TemporaryCredential);

public sealed record PlayerEnrollmentResponse(
    Guid AccountId,
    TemporaryCredentialResponse TemporaryCredential);