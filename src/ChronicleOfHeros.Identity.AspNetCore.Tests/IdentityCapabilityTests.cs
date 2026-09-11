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
    /// <summary>
    /// Tests that the player enrollment process rejects an invalid username, ensuring that the system enforces proper validation rules for usernames during the enrollment process.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Player_enrollment_rejects_an_invalid_username()
    {
        using var scope = fixture.CreateScope();
        var playerAdministrationService = scope.ServiceProvider.GetRequiredService<IPlayerAdministrationService>();

        var result = await playerAdministrationService.EnrollAsync(
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
        using var scope = fixture.CreateScope();
        var playerAdministrationService = scope.ServiceProvider.GetRequiredService<IPlayerAdministrationService>();
        var enrollment = await playerAdministrationService.EnrollAsync(
            new EnrollPlayerRequest("IdentityContractPlayer"),
            TestContext.Current.CancellationToken);

        Assert.Null(enrollment.Failure);
        Assert.NotNull(enrollment.Value);

        var playerIdentityService = scope.ServiceProvider.GetRequiredService<IPlayerIdentityService>();
        var result = await playerIdentityService.GetAsync(
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
        using var scope = fixture.CreateScope();
        var playerAdministrationService = scope.ServiceProvider.GetRequiredService<IPlayerAdministrationService>();
        var enrollment = await playerAdministrationService.EnrollAsync(
            new EnrollPlayerRequest("EnrolledPlayer"),
            TestContext.Current.CancellationToken);

        Assert.Null(enrollment.Failure);
        Assert.NotNull(enrollment.Value);

        var authenticationService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var temporarySignIn = await authenticationService.SignInAsync(
            new SignInRequest("enrolledplayer", enrollment.Value.TemporaryCredential.TemporaryCredential),
            TestContext.Current.CancellationToken);

        Assert.Null(temporarySignIn.Failure);
        Assert.IsType<RestrictedAccessTokenResponse>(temporarySignIn.Value);

        var passwordChange = await authenticationService.ChangePasswordAsync(
            enrollment.Value.AccountId,
            new ChangePasswordRequest(
                enrollment.Value.TemporaryCredential.TemporaryCredential,
                "Replacement-player-password1!"),
            TestContext.Current.CancellationToken);

        Assert.Null(passwordChange.Failure);
        Assert.IsType<TokenPairResponse>(passwordChange.Value);
    }

    /// <summary>
    /// Tests that refresh rotation detects replay, revokes only the replayed session family, and sign-out revokes its session family.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Refresh_rotation_replay_and_sign_out_are_isolated_to_their_sign_in_session_family()
    {
        using var scope = fixture.CreateScope();
        var playerAdministrationService = scope.ServiceProvider.GetRequiredService<IPlayerAdministrationService>();
        var authenticationService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var enrollment = await playerAdministrationService.EnrollAsync(
            new EnrollPlayerRequest("RefreshLifecyclePlayer"),
            TestContext.Current.CancellationToken);

        Assert.Null(enrollment.Failure);
        Assert.NotNull(enrollment.Value);

        var passwordChange = await authenticationService.ChangePasswordAsync(
            enrollment.Value.AccountId,
            new ChangePasswordRequest(
                enrollment.Value.TemporaryCredential.TemporaryCredential,
                "Refresh-lifecycle-password1!"),
            TestContext.Current.CancellationToken);

        Assert.Null(passwordChange.Failure);
        var firstFamily = Assert.IsType<TokenPairResponse>(passwordChange.Value);

        var secondSignIn = await authenticationService.SignInAsync(
            new SignInRequest("RefreshLifecyclePlayer", "Refresh-lifecycle-password1!"),
            TestContext.Current.CancellationToken);

        Assert.Null(secondSignIn.Failure);
        var secondFamily = Assert.IsType<TokenPairResponse>(secondSignIn.Value);

        var firstRefresh = await authenticationService.RefreshAsync(
            new RefreshTokenRequest(firstFamily.RefreshToken),
            TestContext.Current.CancellationToken);

        Assert.Null(firstRefresh.Failure);
        var firstFamilyReplacement = Assert.IsType<TokenPairResponse>(firstRefresh.Value);
        Assert.NotEqual(firstFamily.RefreshToken, firstFamilyReplacement.RefreshToken);

        var replay = await authenticationService.RefreshAsync(
            new RefreshTokenRequest(firstFamily.RefreshToken),
            TestContext.Current.CancellationToken);
        var replayedFamilyRefresh = await authenticationService.RefreshAsync(
            new RefreshTokenRequest(firstFamilyReplacement.RefreshToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(IdentityFailureKind.Unauthorized, replay.Failure?.Kind);
        Assert.Equal(IdentityFailureKind.Unauthorized, replayedFamilyRefresh.Failure?.Kind);

        var secondRefresh = await authenticationService.RefreshAsync(
            new RefreshTokenRequest(secondFamily.RefreshToken),
            TestContext.Current.CancellationToken);

        Assert.Null(secondRefresh.Failure);
        var secondFamilyReplacement = Assert.IsType<TokenPairResponse>(secondRefresh.Value);

        await authenticationService.SignOutAsync(
            new RefreshTokenRequest(secondFamilyReplacement.RefreshToken),
            TestContext.Current.CancellationToken);
        var signedOutFamilyRefresh = await authenticationService.RefreshAsync(
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
        using var scope = fixture.CreateScope();
        var playerAdministrationService = scope.ServiceProvider.GetRequiredService<IPlayerAdministrationService>();
        var authenticationService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var enrollment = await playerAdministrationService.EnrollAsync(
            new EnrollPlayerRequest("PasswordChangePlayer"),
            TestContext.Current.CancellationToken);

        Assert.Null(enrollment.Failure);
        Assert.NotNull(enrollment.Value);

        var initialPasswordChange = await authenticationService.ChangePasswordAsync(
            enrollment.Value.AccountId,
            new ChangePasswordRequest(
                enrollment.Value.TemporaryCredential.TemporaryCredential,
                "Initial-player-password1!"),
            TestContext.Current.CancellationToken);

        Assert.Null(initialPasswordChange.Failure);
        var firstFamily = Assert.IsType<TokenPairResponse>(initialPasswordChange.Value);

        var secondSignIn = await authenticationService.SignInAsync(
            new SignInRequest("PasswordChangePlayer", "Initial-player-password1!"),
            TestContext.Current.CancellationToken);

        Assert.Null(secondSignIn.Failure);
        var secondFamily = Assert.IsType<TokenPairResponse>(secondSignIn.Value);

        var subsequentPasswordChange = await authenticationService.ChangePasswordAsync(
            enrollment.Value.AccountId,
            new ChangePasswordRequest("Initial-player-password1!", "Updated-player-password1!"),
            TestContext.Current.CancellationToken);

        Assert.Null(subsequentPasswordChange.Failure);
        Assert.IsType<TokenPairResponse>(subsequentPasswordChange.Value);

        var firstFamilyRefresh = await authenticationService.RefreshAsync(
            new RefreshTokenRequest(firstFamily.RefreshToken),
            TestContext.Current.CancellationToken);
        var secondFamilyRefresh = await authenticationService.RefreshAsync(
            new RefreshTokenRequest(secondFamily.RefreshToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(IdentityFailureKind.Unauthorized, firstFamilyRefresh.Failure?.Kind);
        Assert.Equal(IdentityFailureKind.Unauthorized, secondFamilyRefresh.Failure?.Kind);
    }

}

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

        var builder = CreateRuntimeHostBuilder();
        builder.AddAspNetCoreIdentity();
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
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:chronicleofheros"] = database.GetConnectionString(),
        });
        builder.AddAspNetCoreIdentity(options => options.EnableMigrations());
        using var migrationHost = builder.Build();
        await migrationHost.StartAsync().ConfigureAwait(false);
        await migrationHost.WaitForShutdownAsync().ConfigureAwait(false);
    }

    private HostApplicationBuilder CreateRuntimeHostBuilder()
    {
        using var signingKey = RSA.Create(2048);
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
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