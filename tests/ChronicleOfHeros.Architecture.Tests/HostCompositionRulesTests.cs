using ChronicleOfHeros.Architecture.Tests.CodeModels;

namespace ChronicleOfHeros.Architecture.Tests;

/// <summary>
/// Verifies that the API and AppHost do not retain the removed authentication capability.
/// </summary>
public sealed class HostCompositionRulesTests
{
#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Verifies that the API currently has no authentication middleware or application endpoints.
    /// </summary>
    [Fact]
    public void Api_has_no_authentication_or_application_endpoints()
    {
        string program = ReadProgram("src", "ChronicleOfHeros.Api");

        Assert.DoesNotContain("AddAuthentication", program, StringComparison.Ordinal);
        Assert.DoesNotContain("UseAuthentication", program, StringComparison.Ordinal);
        Assert.DoesNotContain("UseAuthorization", program, StringComparison.Ordinal);
        Assert.Empty(ApiEndpointModel.Load().Endpoints);
    }

    /// <summary>
    /// Verifies that the AppHost no longer provisions the removed database migration host.
    /// </summary>
    [Fact]
    public void AppHost_does_not_provision_the_removed_migrations_host()
    {
        AppHostModel model = AppHostModel.Load();

        Assert.DoesNotContain(model.Resources, resource => resource.Name == "migrations");
        Assert.DoesNotContain(model.Resources, resource => resource.Name == "postgres");
        Assert.False(File.Exists(ArchitectureTestPaths.FromRoot(
            "src",
            "ChronicleOfHeros.Migrations",
            "ChronicleOfHeros.Migrations.csproj")));
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores

    private static string ReadProgram(params string[] directory) =>
        File.ReadAllText(ArchitectureTestPaths.FromRoot([.. directory, "Program.cs"]));
}
