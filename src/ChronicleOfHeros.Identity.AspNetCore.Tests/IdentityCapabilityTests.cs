using System.Security.Cryptography;

using ChronicleOfHeros.Identity.Contracts;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Testcontainers.PostgreSql;

namespace ChronicleOfHeros.Identity.AspNetCore.Tests;

/// <summary>
/// Tests for the identity capability of the Chronicle of Heros application, including player enrollment and authentication scenarios.
/// </summary>
/// <param name="fixture">The fixture providing the test context and services.</param>
public sealed class IdentityCapabilityTests(IdentityCapabilityFixture fixture)
    : IClassFixture<IdentityCapabilityFixture>
{

#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Tests that the player enrollment process rejects an invalid username, ensuring that the system enforces proper validation rules for usernames during the enrollment process.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Player_enrollment_rejects_an_invalid_username()
    {
        using IServiceScope scope = fixture.CreateScope();
        IPlayerAdministrationService playerAdministrationService = scope.ServiceProvider.GetRequiredService<IPlayerAdministrationService>();

        IdentityOperationResult<PlayerEnrollmentResponse> result = await playerAdministrationService.EnrollAsync(
            new EnrollPlayerRequest("invalid username"),
            TestContext.Current.CancellationToken);

        Assert.Null(result.Value);
        Assert.NotNull(result.Failure);
        Assert.Equal(IdentityFailureKind.Validation, result.Failure.Kind);
        Assert.Contains("username", result.Failure.ValidationErrors!.Keys);
    }

    /// <summary>
    /// Tests that an enrolled Player's immutable account ID can be retrieved through the public Identity contract.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Enrolled_player_identity_is_available_through_the_identity_contract()
    {
        using IServiceScope scope = fixture.CreateScope();
        IPlayerAdministrationService playerAdministrationService = scope.ServiceProvider.GetRequiredService<IPlayerAdministrationService>();
        IdentityOperationResult<PlayerEnrollmentResponse> enrollment = await playerAdministrationService.EnrollAsync(
            new EnrollPlayerRequest("IdentityContractPlayer"),
            TestContext.Current.CancellationToken);

        Assert.Null(enrollment.Failure);
        Assert.NotNull(enrollment.Value);

        IPlayerIdentityService playerIdentityService = scope.ServiceProvider.GetRequiredService<IPlayerIdentityService>();
        IdentityOperationResult<PlayerIdentityResponse> result = await playerIdentityService.GetAsync(
            enrollment.Value.AccountId,
            TestContext.Current.CancellationToken);

        Assert.Null(result.Failure);
        Assert.Equal(enrollment.Value.AccountId, result.Value?.AccountId);
    }

    /// <summary>
    /// Tests that an enrolled player must replace the temporary credential before being able to sign in normally, ensuring that the system enforces the requirement for players to set a permanent password after enrollment.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Enrolled_player_must_replace_the_temporary_credential_before_normal_sign_in()
    {
        using IServiceScope scope = fixture.CreateScope();
        IPlayerAdministrationService playerAdministrationService = scope.ServiceProvider.GetRequiredService<IPlayerAdministrationService>();
        IdentityOperationResult<PlayerEnrollmentResponse> enrollment = await playerAdministrationService.EnrollAsync(
            new EnrollPlayerRequest("EnrolledPlayer"),
            TestContext.Current.CancellationToken);

        Assert.Null(enrollment.Failure);
        Assert.NotNull(enrollment.Value);

        IAuthenticationService authenticationService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        IdentityOperationResult<AccessTokenResponse> temporarySignIn = await authenticationService.SignInAsync(
            new SignInRequest("enrolledplayer", enrollment.Value.TemporaryCredential.TemporaryCredential),
            TestContext.Current.CancellationToken);

        Assert.Null(temporarySignIn.Failure);
        _ = Assert.IsType<RestrictedAccessTokenResponse>(temporarySignIn.Value);

        IdentityOperationResult<TokenPairResponse> passwordChange = await authenticationService.ChangePasswordAsync(
            enrollment.Value.AccountId,
            new ChangePasswordRequest(
                enrollment.Value.TemporaryCredential.TemporaryCredential,
                "Replacement-player-password1!"),
            TestContext.Current.CancellationToken);

        Assert.Null(passwordChange.Failure);
        _ = Assert.IsType<TokenPairResponse>(passwordChange.Value);
    }

    /// <summary>
    /// Tests that refresh rotation detects replay, revokes only the replayed session family, and sign-out revokes its session family.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Refresh_rotation_replay_and_sign_out_are_isolated_to_their_sign_in_session_family()
    {
        using IServiceScope scope = fixture.CreateScope();
        IPlayerAdministrationService playerAdministrationService = scope.ServiceProvider.GetRequiredService<IPlayerAdministrationService>();
        IAuthenticationService authenticationService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        IdentityOperationResult<PlayerEnrollmentResponse> enrollment = await playerAdministrationService.EnrollAsync(
            new EnrollPlayerRequest("RefreshLifecyclePlayer"),
            TestContext.Current.CancellationToken);

        Assert.Null(enrollment.Failure);
        Assert.NotNull(enrollment.Value);

        IdentityOperationResult<TokenPairResponse> passwordChange = await authenticationService.ChangePasswordAsync(
            enrollment.Value.AccountId,
            new ChangePasswordRequest(
                enrollment.Value.TemporaryCredential.TemporaryCredential,
                "Refresh-lifecycle-password1!"),
            TestContext.Current.CancellationToken);

        Assert.Null(passwordChange.Failure);
        TokenPairResponse firstFamily = Assert.IsType<TokenPairResponse>(passwordChange.Value);

        IdentityOperationResult<AccessTokenResponse> secondSignIn = await authenticationService.SignInAsync(
            new SignInRequest("RefreshLifecyclePlayer", "Refresh-lifecycle-password1!"),
            TestContext.Current.CancellationToken);

        Assert.Null(secondSignIn.Failure);
        TokenPairResponse secondFamily = Assert.IsType<TokenPairResponse>(secondSignIn.Value);

        IdentityOperationResult<TokenPairResponse> firstRefresh = await authenticationService.RefreshAsync(
            new RefreshTokenRequest(firstFamily.RefreshToken),
            TestContext.Current.CancellationToken);

        Assert.Null(firstRefresh.Failure);
        TokenPairResponse firstFamilyReplacement = Assert.IsType<TokenPairResponse>(firstRefresh.Value);
        Assert.NotEqual(firstFamily.RefreshToken, firstFamilyReplacement.RefreshToken);

        IdentityOperationResult<TokenPairResponse> replay = await authenticationService.RefreshAsync(
            new RefreshTokenRequest(firstFamily.RefreshToken),
            TestContext.Current.CancellationToken);
        IdentityOperationResult<TokenPairResponse> replayedFamilyRefresh = await authenticationService.RefreshAsync(
            new RefreshTokenRequest(firstFamilyReplacement.RefreshToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(IdentityFailureKind.Unauthorized, replay.Failure?.Kind);
        Assert.Equal(IdentityFailureKind.Unauthorized, replayedFamilyRefresh.Failure?.Kind);

        IdentityOperationResult<TokenPairResponse> secondRefresh = await authenticationService.RefreshAsync(
            new RefreshTokenRequest(secondFamily.RefreshToken),
            TestContext.Current.CancellationToken);

        Assert.Null(secondRefresh.Failure);
        TokenPairResponse secondFamilyReplacement = Assert.IsType<TokenPairResponse>(secondRefresh.Value);

        await authenticationService.SignOutAsync(
            new RefreshTokenRequest(secondFamilyReplacement.RefreshToken),
            TestContext.Current.CancellationToken);
        IdentityOperationResult<TokenPairResponse> signedOutFamilyRefresh = await authenticationService.RefreshAsync(
            new RefreshTokenRequest(secondFamilyReplacement.RefreshToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(IdentityFailureKind.Unauthorized, signedOutFamilyRefresh.Failure?.Kind);
    }

    /// <summary>
    /// Tests that changing a password revokes every previously issued refresh session.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Password_change_revokes_every_existing_refresh_session()
    {
        using IServiceScope scope = fixture.CreateScope();
        IPlayerAdministrationService playerAdministrationService = scope.ServiceProvider.GetRequiredService<IPlayerAdministrationService>();
        IAuthenticationService authenticationService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        IdentityOperationResult<PlayerEnrollmentResponse> enrollment = await playerAdministrationService.EnrollAsync(
            new EnrollPlayerRequest("PasswordChangePlayer"),
            TestContext.Current.CancellationToken);

        Assert.Null(enrollment.Failure);
        Assert.NotNull(enrollment.Value);

        IdentityOperationResult<TokenPairResponse> initialPasswordChange = await authenticationService.ChangePasswordAsync(
            enrollment.Value.AccountId,
            new ChangePasswordRequest(
                enrollment.Value.TemporaryCredential.TemporaryCredential,
                "Initial-player-password1!"),
            TestContext.Current.CancellationToken);

        Assert.Null(initialPasswordChange.Failure);
        TokenPairResponse firstFamily = Assert.IsType<TokenPairResponse>(initialPasswordChange.Value);

        IdentityOperationResult<AccessTokenResponse> secondSignIn = await authenticationService.SignInAsync(
            new SignInRequest("PasswordChangePlayer", "Initial-player-password1!"),
            TestContext.Current.CancellationToken);

        Assert.Null(secondSignIn.Failure);
        TokenPairResponse secondFamily = Assert.IsType<TokenPairResponse>(secondSignIn.Value);

        IdentityOperationResult<TokenPairResponse> subsequentPasswordChange = await authenticationService.ChangePasswordAsync(
            enrollment.Value.AccountId,
            new ChangePasswordRequest("Initial-player-password1!", "Updated-player-password1!"),
            TestContext.Current.CancellationToken);

        Assert.Null(subsequentPasswordChange.Failure);
        _ = Assert.IsType<TokenPairResponse>(subsequentPasswordChange.Value);

        IdentityOperationResult<TokenPairResponse> firstFamilyRefresh = await authenticationService.RefreshAsync(
            new RefreshTokenRequest(firstFamily.RefreshToken),
            TestContext.Current.CancellationToken);
        IdentityOperationResult<TokenPairResponse> secondFamilyRefresh = await authenticationService.RefreshAsync(
            new RefreshTokenRequest(secondFamily.RefreshToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(IdentityFailureKind.Unauthorized, firstFamilyRefresh.Failure?.Kind);
        Assert.Equal(IdentityFailureKind.Unauthorized, secondFamilyRefresh.Failure?.Kind);
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores

}

// xUnit requires public types for fixtures.
#pragma warning disable CA1515 // Consider making public types internal

/// <summary>
/// Fixture for the identity capability tests, providing a PostgreSQL database container and a runtime host with the identity services registered, ensuring that the tests have a consistent and isolated environment for testing the identity functionality.
/// </summary>
public sealed class IdentityCapabilityFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17.2").Build();
    private IHost? runtimeHost;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        await database.StartAsync().ConfigureAwait(false);
        await MigrateDatabaseAsync().ConfigureAwait(false);

        HostApplicationBuilder builder = CreateRuntimeHostBuilder();
        _ = builder.AddAspNetCoreIdentity();
        runtimeHost = builder.Build();
        await runtimeHost.StartAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new service scope from the runtime host, allowing for the resolution of services and dependencies within the scope of the identity capability tests, ensuring that each test has its own isolated context for service resolution.
    /// </summary>
    /// <returns>A new service scope for resolving services and dependencies.</returns>
    public IServiceScope CreateScope() => runtimeHost!.Services.CreateScope();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (runtimeHost is not null)
        {
            await runtimeHost.StopAsync().ConfigureAwait(false);
            runtimeHost.Dispose();
        }

        await database.DisposeAsync().ConfigureAwait(false);
    }

    private async Task MigrateDatabaseAsync()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:chronicleofheros"] = database.GetConnectionString(),
        });
        _ = builder.AddAspNetCoreIdentity(options => options.EnableMigrations());
        using IHost migrationHost = builder.Build();
        await migrationHost.StartAsync().ConfigureAwait(false);
        await migrationHost.WaitForShutdownAsync().ConfigureAwait(false);
    }

    private HostApplicationBuilder CreateRuntimeHostBuilder()
    {
        using RSA signingKey = RSA.Create(2048);
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:chronicleofheros"] = database.GetConnectionString(),
            ["Jwt:SigningPrivateKey"] = Convert.ToBase64String(signingKey.ExportPkcs8PrivateKey()),
            ["Jwt:Issuer"] = "https://identity.chronicleofheros.test",
            ["Jwt:Audience"] = "identity-capability-tests",
            ["BootstrapOperator:Username"] = "FixtureOperator",
            ["BootstrapOperator:TemporaryPassword"] = "Fixture-operator-temporary-password1!",
        });

        return builder;
    }
}

#pragma warning restore CA1515 // Consider making public types internal