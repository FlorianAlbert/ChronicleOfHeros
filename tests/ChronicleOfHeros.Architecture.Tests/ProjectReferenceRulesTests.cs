using System.Xml.Linq;

using ChronicleOfHeros.Architecture.Tests.CodeModels;
using ChronicleOfHeros.Architecture.Tests.VerifiedViews;
using ChronicleOfHeros.Identity.Contracts;

namespace ChronicleOfHeros.Architecture.Tests;

/// <summary>
/// Verifies the solution's permitted direct project references and assembly visibility settings.
/// </summary>
public sealed class ProjectReferenceRulesTests
{
#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Verifies that the web frontend is a single server project without a WebAssembly host.
    /// </summary>
    [Fact]
    public void Web_frontend_is_a_single_server_project()
    {
        string webProject = ArchitectureTestPaths.FromRoot("src", "ChronicleOfHeros.Web", "ChronicleOfHeros.Web.csproj");
        string clientProject = ArchitectureTestPaths.FromRoot("src", "ChronicleOfHeros.Web.Client", "ChronicleOfHeros.Web.Client.csproj");
        XDocument project = XDocument.Load(webProject);
        XDocument solution = XDocument.Load(ArchitectureTestPaths.FromRoot("ChronicleOfHeros.slnx"));

        Assert.False(File.Exists(clientProject));
        Assert.Equal(
            (IEnumerable<string>)DocumentedReferenceTargets(WebProjectName),
            [.. GetProjectReferences(SolutionProjects.Named(WebProjectName).Path).Select(Path.GetFileNameWithoutExtension).OfType<string>()]);
        Assert.DoesNotContain(
            project.Descendants("PackageReference"),
            reference => reference.Attribute("Include")?.Value.Contains("WebAssembly", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(
            solution.Descendants("Project"),
            reference => reference.Attribute("Path")?.Value.Contains("ChronicleOfHeros.Web.Client", StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// Verifies that only executable hosts and test projects reference the Identity implementation, and exactly those
    /// the <c>project-references</c> view documents.
    /// </summary>
    [Fact]
    public void Identity_implementation_references_are_limited_to_hosts_and_tests()
    {
        string implementationProject = SolutionProjects.Named(IdentityImplementationProjectName).Path;

        string[] referencingProjects = GetProjectsReferencing(implementationProject);

        Assert.Equal(DocumentedReferenceSources(IdentityImplementationProjectName), referencingProjects.Select(Path.GetFileNameWithoutExtension));
        Assert.All(referencingProjects, project =>
        {
            SolutionProject solutionProject = SolutionProjects.Named(Path.GetFileNameWithoutExtension(project));
            Assert.True(
                solutionProject.IsTestProject || solutionProject.IsExecutableHost,
                $"{project} references the Identity implementation but is neither an executable host nor a test project.");
        });
    }

    /// <summary>
    /// Verifies that contracts do not reference a capability implementation or any other project.
    /// </summary>
    [Fact]
    public void Identity_contracts_project_has_no_project_references()
    {
        string[] referencedProjects = [.. GetProjectReferences(
            "src/ChronicleOfHeros.Identity.Contracts/ChronicleOfHeros.Identity.Contracts.csproj")];

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
            .Select(ArchitectureTestPaths.ToRepositoryPath)];

        Assert.Empty(sourceFilesGrantingInternalAccess);
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores

    private const string WebProjectName = "ChronicleOfHeros.Web";
    private const string IdentityImplementationProjectName = "ChronicleOfHeros.Identity.AspNetCore";

    private static FlowchartView DocumentedReferences =>
        FlowchartView.Parse(ArchitectureDocuments.Repository.Single(VerifiedViewCheckers.ProjectReferences));

    private static string[] DocumentedReferenceTargets(string source) =>
        [.. DocumentedReferences.Edges
            .Where(edge => edge.Source == source)
            .Select(edge => edge.Target)
            .Order(StringComparer.Ordinal)];

    private static string[] DocumentedReferenceSources(string target) =>
        [.. DocumentedReferences.Edges
            .Where(edge => edge.Target == target)
            .Select(edge => edge.Source)
            .Order(StringComparer.Ordinal)];

    private static string[] GetProjectsReferencing(string referencedProject) =>
        [.. Directory.GetFiles(ArchitectureTestPaths.RepositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(ArchitectureTestPaths.ToRepositoryPath)
            .Where(project => !project.Split('/').Any(segment => segment is "artifacts" or "bin" or "obj"))
            .Where(project => GetProjectReferences(project).Contains(referencedProject, StringComparer.Ordinal))
            .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.Ordinal)];

    private static IReadOnlyList<string> GetProjectReferences(string project) => SolutionProjects.ReferencePaths(project);
}