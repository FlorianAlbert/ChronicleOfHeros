using ChronicleOfHeros.Identity.AspNetCore;
using ChronicleOfHeros.Identity.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;
using Testcontainers.PostgreSql;

namespace ChronicleOfHeros.Identity.AspNetCore.Tests;

public sealed class IdentityCapabilityTests(IdentityCapabilityFixture fixture)
    : IClassFixture<IdentityCapabilityFixture>
{
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

}

public sealed class IdentityCapabilityFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17.2").Build();
    private IHost? runtimeHost;

    public async ValueTask InitializeAsync()
    {
        await database.StartAsync();
        await MigrateDatabaseAsync();

        var builder = CreateHostBuilder();
        builder.AddAspNetCoreIdentity();
        runtimeHost = builder.Build();
        await runtimeHost.StartAsync();
    }

    public IServiceScope CreateScope() => runtimeHost!.Services.CreateScope();

    public async ValueTask DisposeAsync()
    {
        if (runtimeHost is not null)
        {
            await runtimeHost.StopAsync();
            runtimeHost.Dispose();
        }

        await database.DisposeAsync();
    }

    private async Task MigrateDatabaseAsync()
    {
        var builder = CreateHostBuilder();
        builder.AddAspNetCoreIdentity(options => options.EnableMigrations());
        using var migrationHost = builder.Build();
        await migrationHost.StartAsync();
        await migrationHost.WaitForShutdownAsync();
    }

    private HostApplicationBuilder CreateHostBuilder()
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