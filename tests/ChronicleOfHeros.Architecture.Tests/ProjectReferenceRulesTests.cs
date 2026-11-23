using System.Xml.Linq;

using ChronicleOfHeros.Architecture.Tests.CodeModels;
using ChronicleOfHeros.Architecture.Tests.VerifiedViews;

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

#pragma warning restore CA1707 // Identifiers should not contain underscores

    private const string WebProjectName = "ChronicleOfHeros.Web";

    private static FlowchartView DocumentedReferences =>
        FlowchartView.Parse(ArchitectureDocuments.Repository.Single(VerifiedViewCheckers.ProjectReferences));

    private static string[] DocumentedReferenceTargets(string source) =>
        [.. DocumentedReferences.Edges
            .Where(edge => edge.Source == source)
            .Select(edge => edge.Target)
            .Order(StringComparer.Ordinal)];

    private static IReadOnlyList<string> GetProjectReferences(string project) => SolutionProjects.ReferencePaths(project);
}