using ChronicleOfHeros.Identity.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace ChronicleOfHeros.Identity.AspNetCore.Tests;

/// <summary>
/// Tests for the <see cref="AspNetCoreIdentityRegistration"/> class and its related functionality.
/// </summary>
public sealed class AspNetCoreIdentityRegistrationTests
{
    /// <summary>
    /// Tests that the contracts assembly does not reference any ASP.NET Core or persistence-related assemblies, ensuring that the contracts remain independent of specific frameworks or implementations.
    /// </summary>
    [Fact]
    public void Contracts_reference_no_asp_net_core_or_persistence_assemblies()
    {
        var referencedAssemblies = typeof(IAuthenticationService).Assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name);

        Assert.DoesNotContain(
            referencedAssemblies,
            name => name is not null
                && (name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                    || name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                    || name.StartsWith("Microsoft.IdentityModel", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Tests that the <see cref="AspNetCoreIdentityRegistration"/> implementation exposes only its intended public surface, ensuring that no unintended types are publicly accessible.
    /// </summary>
    [Fact]
    public void Implementation_exposes_only_its_registration_surface()
    {
        var publicTypeNames = typeof(AspNetCoreIdentityRegistration).Assembly
            .GetExportedTypes()
            .Where(type => !type.IsNested)
            .Select(type => type.FullName!)
            .OrderBy(typeName => typeName)
            .ToArray();

        Assert.Equal(
            [
                typeof(AspNetCoreIdentityRegistration).FullName!,
                typeof(AspNetCoreIdentityRegistrationOptions).FullName!,
            ],
            publicTypeNames);
    }

    /// <summary>
    /// Tests that the runtime registration of the identity services rejects missing JWT configuration at startup, ensuring that the necessary configuration is provided for proper operation.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Runtime_registration_rejects_missing_jwt_configuration_at_startup()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:chronicleofheros"] = "Host=localhost;Database=identity-tests;Username=postgres;Password=postgres",
        });

        builder.AddAspNetCoreIdentity();
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("JWT configuration is required.", exception.Message, StringComparison.Ordinal);
    }
    
    /// <summary>
    /// Tests that the runtime registration of the identity services exposes the expected contracts without requiring any host-assembled dependencies, ensuring that the services can be resolved and used independently of the host's assembly context.
    /// </summary>
    [Fact]
    public void Registration_exposes_the_identity_contracts_without_host_assembled_dependencies()
    {
        using var signingKey = RSA.Create(2048);
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:chronicleofheros"] = "Host=localhost;Database=identity-tests;Username=postgres;Password=postgres",
            ["Jwt:SigningPrivateKey"] = Convert.ToBase64String(signingKey.ExportPkcs8PrivateKey()),
            ["Jwt:Issuer"] = "https://identity.chronicleofheros.test",
            ["Jwt:Audience"] = "identity-tests",
        });

        builder.AddAspNetCoreIdentity();

        using var services = builder.Services.BuildServiceProvider();

        Assert.IsType<IAuthenticationService>(
            services.GetRequiredService<IAuthenticationService>(), exactMatch: false);
        Assert.IsType<IPlayerAdministrationService>(
            services.GetRequiredService<IPlayerAdministrationService>(), exactMatch: false);
    }
}