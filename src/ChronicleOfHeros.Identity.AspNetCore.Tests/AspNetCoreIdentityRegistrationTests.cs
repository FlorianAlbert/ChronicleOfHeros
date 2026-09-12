using System.Security.Cryptography;

using ChronicleOfHeros.Identity.Contracts;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ChronicleOfHeros.Identity.AspNetCore.Tests;

/// <summary>
/// Tests for the <see cref="AspNetCoreIdentityRegistration"/> class and its related functionality.
/// </summary>
public sealed class AspNetCoreIdentityRegistrationTests
{

#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Tests that the runtime registration of the identity services rejects missing JWT configuration at startup, ensuring that the necessary configuration is provided for proper operation.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Runtime_registration_rejects_missing_jwt_configuration_at_startup()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:chronicleofheros"] = "Host=localhost;Database=identity-tests;Username=postgres;Password=postgres",
        });

        _ = builder.AddAspNetCoreIdentity();
        using IHost host = builder.Build();

        OptionsValidationException exception = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("JWT configuration is required.", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that the runtime registration of the identity services exposes the expected contracts without requiring any host-assembled dependencies, ensuring that the services can be resolved and used independently of the host's assembly context.
    /// </summary>
    [Fact]
    public void Registration_exposes_the_identity_contracts_without_host_assembled_dependencies()
    {
        using RSA signingKey = RSA.Create(2048);
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:chronicleofheros"] = "Host=localhost;Database=identity-tests;Username=postgres;Password=postgres",
            ["Jwt:SigningPrivateKey"] = Convert.ToBase64String(signingKey.ExportPkcs8PrivateKey()),
            ["Jwt:Issuer"] = "https://identity.chronicleofheros.test",
            ["Jwt:Audience"] = "identity-tests",
        });

        _ = builder.AddAspNetCoreIdentity();

        using ServiceProvider services = builder.Services.BuildServiceProvider();

        _ = Assert.IsType<IAuthenticationService>(
            services.GetRequiredService<IAuthenticationService>(), exactMatch: false);
        _ = Assert.IsType<IPlayerAdministrationService>(
            services.GetRequiredService<IPlayerAdministrationService>(), exactMatch: false);
        _ = Assert.IsType<IPlayerIdentityService>(
            services.GetRequiredService<IPlayerIdentityService>(), exactMatch: false);
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores

}