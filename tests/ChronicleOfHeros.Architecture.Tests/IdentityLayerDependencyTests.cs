using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;

using ChronicleOfHeros.Identity.AspNetCore;
using ChronicleOfHeros.Identity.Contracts;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ChronicleOfHeros.Architecture.Tests;

/// <summary>
/// Verifies the internal layer dependency direction of the Identity implementation.
/// </summary>
public sealed class IdentityLayerDependencyTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader().LoadAssemblies(
        typeof(AspNetCoreIdentityRegistration).Assembly,
        typeof(IAuthenticationService).Assembly).Build();

#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Verifies that Domain does not depend on application, infrastructure, or dependency injection layers.
    /// </summary>
    [Fact]
    public void Domain_does_not_depend_on_other_implementation_layers()
    {
        IObjectProvider<IType> domain = Types().That().ResideInNamespace("ChronicleOfHeros.Identity.AspNetCore.Domain");

        AssertDoesNotDependOn(domain, ApplicationLayer, "Domain is framework-free and independent");
        AssertDoesNotDependOn(domain, InfrastructureLayer, "Domain is framework-free and independent");
        AssertDoesNotDependOn(domain, DependencyInjectionLayer, "Domain is framework-free and independent");
    }

    /// <summary>
    /// Verifies that Application does not depend on Infrastructure or DependencyInjection.
    /// </summary>
    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_dependency_injection()
    {
        AssertDoesNotDependOn(ApplicationLayer, InfrastructureLayer, "Application uses private ports rather than infrastructure");
        AssertDoesNotDependOn(ApplicationLayer, DependencyInjectionLayer, "Dependency injection is the sole assembly point");
    }

    /// <summary>
    /// Verifies that Infrastructure does not depend on DependencyInjection.
    /// </summary>
    [Fact]
    public void Infrastructure_does_not_depend_on_dependency_injection()
    {
        AssertDoesNotDependOn(InfrastructureLayer, DependencyInjectionLayer, "Dependency injection is the sole assembly point");
    }

    /// <summary>
    /// Verifies that every layer has owned source files and that service assembly occurs only in DependencyInjection.
    /// </summary>
    [Fact]
    public void Identity_layers_are_present_and_service_assembly_is_limited_to_dependency_injection()
    {
        string identityProject = ArchitectureTestPaths.FromRoot("src", "ChronicleOfHeros.Identity.AspNetCore");
        string[] layerDirectories = ["Domain", "Application", "Infrastructure", "DependencyInjection"];

        Assert.All(layerDirectories, layer =>
            Assert.NotEmpty(Directory.GetFiles(Path.Combine(identityProject, layer), "*.cs", SearchOption.AllDirectories)));

        AssertFrameworkFree(Path.Combine(identityProject, "Domain"));
        AssertFrameworkFree(Path.Combine(identityProject, "Application"));

        string[] nonAssemblySources = [.. Directory.GetFiles(identityProject, "*.cs", SearchOption.AllDirectories)
            .Where(source => !source.Contains($"{Path.DirectorySeparatorChar}DependencyInjection{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !source.EndsWith("AspNetCoreIdentityRegistration.cs", StringComparison.Ordinal))];

        Assert.All(nonAssemblySources, source =>
        {
            string content = File.ReadAllText(source);
            Assert.DoesNotContain("AddScoped", content, StringComparison.Ordinal);
            Assert.DoesNotContain("AddSingleton", content, StringComparison.Ordinal);
            Assert.DoesNotContain("AddHostedService", content, StringComparison.Ordinal);
        });
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores

    private static IObjectProvider<IType> ApplicationLayer =>
        Types().That().ResideInNamespace("ChronicleOfHeros.Identity.AspNetCore.Application");

    private static IObjectProvider<IType> InfrastructureLayer =>
        Types().That().HaveFullNameContaining("ChronicleOfHeros.Identity.AspNetCore.Infrastructure");

    private static IObjectProvider<IType> DependencyInjectionLayer =>
        Types().That().ResideInNamespace("ChronicleOfHeros.Identity.AspNetCore.DependencyInjection");

    private static void AssertDoesNotDependOn(
        IObjectProvider<IType> sourceLayer,
        IObjectProvider<IType> forbiddenLayer,
        string reason)
    {
        IArchRule rule = Types().That().Are(sourceLayer).Should().NotDependOnAny(forbiddenLayer)
            .Because(reason)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    private static void AssertFrameworkFree(string layerDirectory)
    {
        string[] sourceFiles = Directory.GetFiles(layerDirectory, "*.cs", SearchOption.AllDirectories);

        Assert.All(sourceFiles, source =>
            Assert.DoesNotContain("using Microsoft.", File.ReadAllText(source), StringComparison.Ordinal));
    }
}