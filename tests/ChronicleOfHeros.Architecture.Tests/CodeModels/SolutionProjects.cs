using System.Reflection;
using System.Xml.Linq;

namespace ChronicleOfHeros.Architecture.Tests.CodeModels;

/// <summary>
/// A project listed in <c>ChronicleOfHeros.slnx</c>.
/// </summary>
/// <param name="Name">The project file name without <c>.csproj</c>.</param>
/// <param name="Path">The repository-relative project file path with <c>/</c> separators.</param>
internal sealed record SolutionProject(string Name, string Path)
{
    /// <summary>
    /// Gets a value indicating whether this is a test project.
    /// </summary>
    internal bool IsTestProject => Name.EndsWith(".Tests", StringComparison.Ordinal);

    /// <summary>
    /// Gets a value indicating whether this project builds an executable host (web or AppHost SDK, or <c>OutputType</c> <c>Exe</c>) and is not a test project.
    /// </summary>
    internal bool IsExecutableHost
    {
        get
        {
            XElement project = Load().Root!;
            string sdk = project.Attribute("Sdk")?.Value ?? string.Empty;
            return !IsTestProject
                && (sdk.StartsWith("Microsoft.NET.Sdk.Web", StringComparison.Ordinal)
                    || sdk.StartsWith("Aspire.AppHost.Sdk", StringComparison.Ordinal)
                    || project.Descendants("OutputType").Any(outputType => outputType.Value.Trim() == "Exe"));
        }
    }

    /// <summary>
    /// Loads the project file.
    /// </summary>
    internal XDocument Load() => XDocument.Load(FullPath);

    /// <summary>
    /// Gets the absolute project file path.
    /// </summary>
    internal string FullPath => ArchitectureTestPaths.FromRoot([.. Path.Split('/')]);

    /// <summary>
    /// Loads the project's compiled assembly, which the architecture test project must reference.
    /// </summary>
    internal Assembly LoadAssembly() => Assembly.Load(Name);
}

/// <summary>
/// Reads the solution's projects and their project references per docs/architecture/conventions.md § <c>project-references</c>.
/// </summary>
internal static class SolutionProjects
{
    /// <summary>The repository-relative path of the solution file.</summary>
    internal const string SolutionPath = "ChronicleOfHeros.slnx";

    private static readonly Lazy<IReadOnlyList<SolutionProject>> Projects = new(LoadProjects);

    /// <summary>
    /// Gets every project in the solution, at any folder depth.
    /// </summary>
    internal static IReadOnlyList<SolutionProject> All => Projects.Value;

    /// <summary>
    /// Gets the production (non-test) projects.
    /// </summary>
    internal static IEnumerable<SolutionProject> Production => All.Where(project => !project.IsTestProject);

    /// <summary>
    /// Returns the project with the given name.
    /// </summary>
    internal static SolutionProject Named(string name) =>
        All.SingleOrDefault(project => project.Name == name)
            ?? throw new InvalidOperationException($"{SolutionPath} lists no project named '{name}'.");

    /// <summary>
    /// Returns the repository-relative paths, with <c>/</c> separators, of the project's direct project references.
    /// </summary>
    internal static IReadOnlyList<string> ReferencePaths(string projectPath)
    {
        string fullPath = ArchitectureTestPaths.FromRoot([.. projectPath.Replace('\\', '/').Split('/')]);
        string directory = System.IO.Path.GetDirectoryName(fullPath)!;
        return [.. XDocument.Load(fullPath)
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .Select(include => ArchitectureTestPaths.ToRepositoryPath(ArchitectureTestPaths.ResolveFrom(directory, include)))
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Returns every solution project-reference edge as (source name, target name), and errors for references outside the solution.
    /// </summary>
    internal static (IReadOnlyList<(string Source, string Target)> Edges, IReadOnlyList<string> Errors) References()
    {
        Dictionary<string, SolutionProject> byPath = All.ToDictionary(project => project.Path, StringComparer.Ordinal);
        List<(string Source, string Target)> edges = [];
        List<string> errors = [];
        foreach (SolutionProject project in All)
        {
            foreach (string reference in ReferencePaths(project.Path))
            {
                if (byPath.TryGetValue(reference, out SolutionProject? target))
                {
                    edges.Add((project.Name, target.Name));
                }
                else
                {
                    errors.Add($"{project.Path}: references '{reference}', which is not in {SolutionPath}.");
                }
            }
        }

        return (edges, errors);
    }

    private static IReadOnlyList<SolutionProject> LoadProjects() =>
        [.. XDocument.Load(ArchitectureTestPaths.FromRoot(SolutionPath))
            .Descendants("Project")
            .Select(element => element.Attribute("Path")?.Value)
            .OfType<string>()
            .Select(path => ArchitectureTestPaths.ToRepositoryPath(ArchitectureTestPaths.ResolveFrom(ArchitectureTestPaths.RepositoryRoot, path)))
            .Select(path => new SolutionProject(System.IO.Path.GetFileNameWithoutExtension(path), path))
            .OrderBy(project => project.Name, StringComparer.Ordinal)];
}
