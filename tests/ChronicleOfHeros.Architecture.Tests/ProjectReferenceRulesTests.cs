using System.Xml.Linq;

using ChronicleOfHeros.Identity.Contracts;

namespace ChronicleOfHeros.Architecture.Tests;

/// <summary>
/// Verifies the solution's permitted direct project references and assembly visibility settings.
/// </summary>
public sealed class ProjectReferenceRulesTests
{
#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Verifies that only executable hosts and Identity test projects reference the Identity implementation.
    /// </summary>
    [Fact]
    public void Identity_implementation_references_are_limited_to_hosts_and_tests()
    {
        string implementationProject = "src/ChronicleOfHeros.Identity.AspNetCore/ChronicleOfHeros.Identity.AspNetCore.csproj";

        string[] referencingProjects = GetProjectsReferencing(implementationProject);

        Assert.Equal(
            [
                "src/ChronicleOfHeros.Api/ChronicleOfHeros.Api.csproj",
                "src/ChronicleOfHeros.Identity.AspNetCore.Tests/ChronicleOfHeros.Identity.AspNetCore.Tests.csproj",
                "src/ChronicleOfHeros.Migrations/ChronicleOfHeros.Migrations.csproj",
                "tests/ChronicleOfHeros.Architecture.Tests/ChronicleOfHeros.Architecture.Tests.csproj",
            ],
            referencingProjects);
    }

    /// <summary>
    /// Verifies that contracts do not reference a capability implementation or any other project.
    /// </summary>
    [Fact]
    public void Identity_contracts_project_has_no_project_references()
    {
        string[] referencedProjects = GetProjectReferences(
            "src/ChronicleOfHeros.Identity.Contracts/ChronicleOfHeros.Identity.Contracts.csproj");

        Assert.Empty(referencedProjects);
    }

    /// <summary>
    /// Verifies that the Identity contracts assembly does not reference implementation frameworks.
    /// </summary>
    [Fact]
    public void Identity_contracts_reference_no_implementation_frameworks()
    {
        string[] referencedAssemblyNames = [.. typeof(IAuthenticationService).Assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .OfType<string>()
            .OrderBy(assemblyName => assemblyName, StringComparer.Ordinal)];

        Assert.DoesNotContain(
            referencedAssemblyNames,
            assemblyName => assemblyName.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                || assemblyName.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                || assemblyName.StartsWith("Microsoft.IdentityModel", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that implementation tests are not granted access to Identity internals.
    /// </summary>
    [Fact]
    public void Identity_implementation_does_not_grant_InternalsVisibleTo()
    {
        string identityProjectDirectory = ArchitectureTestPaths.FromRoot("src", "ChronicleOfHeros.Identity.AspNetCore");

        string[] sourceFiles = Directory.GetFiles(identityProjectDirectory, "*.cs", SearchOption.AllDirectories);
        string[] sourceFilesGrantingInternalAccess = [.. sourceFiles
            .Where(file => File.ReadAllText(file).Contains("InternalsVisibleTo", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(ArchitectureTestPaths.RepositoryRoot, file))];

        Assert.Empty(sourceFilesGrantingInternalAccess);
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores

    private static string[] GetProjectsReferencing(string referencedProject) =>
        [.. Directory.GetFiles(ArchitectureTestPaths.RepositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(project => GetProjectReferences(project).Contains(referencedProject, StringComparer.Ordinal))
            .Select(project => Path.GetRelativePath(ArchitectureTestPaths.RepositoryRoot, project))
            .OrderBy(project => project, StringComparer.Ordinal)];

    private static string[] GetProjectReferences(string project)
    {
        string projectPath = Path.IsPathRooted(project)
            ? project
            : ArchitectureTestPaths.FromRoot(project);

        return [.. XDocument.Load(projectPath)
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => include is not null)
            .Select(include => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, include!)))
            .Select(include => Path.GetRelativePath(ArchitectureTestPaths.RepositoryRoot, include))
            .OrderBy(include => include, StringComparer.Ordinal)];
    }
}