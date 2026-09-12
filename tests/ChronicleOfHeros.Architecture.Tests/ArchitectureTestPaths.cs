namespace ChronicleOfHeros.Architecture.Tests;

internal static class ArchitectureTestPaths
{
    internal static string RepositoryRoot { get; } = FindRepositoryRoot();

    internal static string FromRoot(params string[] segments) => Path.Combine([RepositoryRoot, .. segments]);

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