namespace ChronicleOfHeros.Architecture.Tests;

internal static class ArchitectureTestPaths
{
    internal static string RepositoryRoot { get; } = FindRepositoryRoot();

    internal static string FromRoot(params string[] segments) => Path.Combine([RepositoryRoot, .. segments]);

    /// <summary>
    /// Converts an absolute path to a repository-relative path with <c>/</c> separators on every platform.
    /// </summary>
    internal static string ToRepositoryPath(string path) =>
        Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/');

    /// <summary>
    /// Resolves a path written in a project or solution file, accepting <c>/</c> and <c>\</c> alike.
    /// </summary>
    internal static string ResolveFrom(string baseDirectory, string relativePath) =>
        Path.GetFullPath(Path.Combine(baseDirectory, relativePath.Replace('\\', '/')));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ChronicleOfHeros.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}