using ChronicleOfHeros.Architecture.Tests.CodeModels;

namespace ChronicleOfHeros.Architecture.Tests;

/// <summary>
/// Verifies that the API and AppHost do not retain removed capabilities.
/// </summary>
public sealed class HostCompositionRulesTests
{
#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Verifies that the API currently has no application endpoints.
    /// </summary>
    [Fact]
    public void Api_has_no_application_endpoints() =>
        Assert.Empty(ApiEndpointModel.Load().Endpoints);

    /// <summary>
    /// Verifies that the AppHost no longer provisions the removed database migration host.
    /// </summary>
    [Fact]
    public void AppHost_does_not_provision_the_removed_migrations_host()
    {
        AppHostModel model = AppHostModel.Load();

        Assert.DoesNotContain(model.Resources, resource => resource.Name == "migrations");
        Assert.False(File.Exists(ArchitectureTestPaths.FromRoot(
            "src",
            "ChronicleOfHeros.Migrations",
            "ChronicleOfHeros.Migrations.csproj")));
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores
}
