namespace ChronicleOfHeros.Architecture.Tests;

/// <summary>
/// Verifies that executable hosts remain capability composition and transport adapters.
/// </summary>
public sealed class HostCompositionRulesTests
{
#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Verifies that the API registers Identity exactly once without enabling migrations.
    /// </summary>
    [Fact]
    public void Api_installs_Identity_once_without_enabling_migrations()
    {
        string program = ReadProgram("src", "ChronicleOfHeros.Api");

        Assert.Equal(1, Count(program, "builder.AddAspNetCoreIdentity("));
        Assert.DoesNotContain("EnableMigrations", program, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that only the migrations host enables Identity migrations through registration.
    /// </summary>
    [Fact]
    public void Migrations_host_enables_Identity_migrations_through_its_registration_call()
    {
        string program = ReadProgram("src", "ChronicleOfHeros.Migrations");

        Assert.Equal(1, Count(program, "builder.AddAspNetCoreIdentity("));
        Assert.Contains("options => options.EnableMigrations()", program, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the public registration adapter delegates assembly to the DependencyInjection layer.
    /// </summary>
    [Fact]
    public void Public_Identity_registration_does_not_assemble_services()
    {
        string registration = File.ReadAllText(ArchitectureTestPaths.FromRoot(
            "src",
            "ChronicleOfHeros.Identity.AspNetCore",
            "AspNetCoreIdentityRegistration.cs"));

        Assert.Contains("IdentityCapabilityRegistration.Register", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("builder.Services", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("AddNpgsqlDbContext", registration, StringComparison.Ordinal);
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores

    private static string ReadProgram(params string[] directory) =>
        File.ReadAllText(ArchitectureTestPaths.FromRoot([.. directory, "Program.cs"]));

    private static int Count(string value, string needle) => value.Split(needle, StringSplitOptions.None).Length - 1;
}