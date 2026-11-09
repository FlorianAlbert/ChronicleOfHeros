using System.Text.RegularExpressions;

namespace ChronicleOfHeros.Architecture.Tests.VerifiedViews;

/// <summary>
/// The verified views and marker format errors found in a set of architecture documents.
/// </summary>
internal sealed record VerifiedViewCatalog(IReadOnlyList<VerifiedView> Views, IReadOnlyList<string> FormatErrors)
{
    /// <summary>
    /// Returns the single view with the given id, or throws with a message naming every location found.
    /// </summary>
    internal VerifiedView Single(string id)
    {
        VerifiedView[] matches = [.. Views.Where(view => view.Id == id)];
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(matches.Length == 0
                ? $"No verified view '{id}' was found in ARCHITECTURE.md or docs/architecture/**/*.md (see docs/architecture/conventions.md § View catalogue)."
                : $"Verified view '{id}' appears {matches.Length} times: {string.Join(", ", matches.Select(view => view.Location))}.");
    }
}

/// <summary>
/// Discovers architecture documents and extracts their verified views as docs/architecture/conventions.md § Markers specifies.
/// </summary>
internal static partial class ArchitectureDocuments
{
    private const string IdPattern = "(?<id>[a-z0-9-]+(:[A-Za-z0-9-]+)?)";

    private static readonly Lazy<VerifiedViewCatalog> RepositoryCatalog = new(LoadRepositoryCatalog);

    /// <summary>
    /// Gets the verified views of the repository's architecture documents.
    /// </summary>
    internal static VerifiedViewCatalog Repository => RepositoryCatalog.Value;

    /// <summary>
    /// Gets the repository-relative paths, with <c>/</c> separators, of every scanned document.
    /// </summary>
    internal static IReadOnlyList<string> DocumentPaths() =>
        [.. Directory.GetFiles(ArchitectureTestPaths.FromRoot("docs", "architecture"), "*.md", SearchOption.AllDirectories)
            .Prepend(ArchitectureTestPaths.FromRoot("ARCHITECTURE.md"))
            .Select(ArchitectureTestPaths.ToRepositoryPath)
            .OrderBy(path => path, StringComparer.Ordinal)];

    /// <summary>
    /// Reads a repository document by its repository-relative path.
    /// </summary>
    internal static string Read(string repositoryPath) =>
        File.ReadAllText(ArchitectureTestPaths.FromRoot([.. repositoryPath.Split('/')]));

    /// <summary>
    /// Extracts the verified views of the given documents.
    /// </summary>
    /// <param name="documents">Pairs of repository-relative path and document text.</param>
    internal static VerifiedViewCatalog Extract(IEnumerable<(string Path, string Text)> documents)
    {
        List<VerifiedView> views = [];
        List<string> errors = [];
        foreach ((string path, string text) in documents)
        {
            ExtractDocument(path, text, views, errors);
        }

        return new VerifiedViewCatalog(views, errors);
    }

    /// <summary>
    /// Splits text into numbered lines, stripping a trailing carriage return from each.
    /// </summary>
    internal static DocumentLine[] SplitLines(string text) =>
        [.. text.Split('\n').Select((line, index) => new DocumentLine(index + 1, line.TrimEnd('\r')))];

    private static VerifiedViewCatalog LoadRepositoryCatalog() =>
        Extract(DocumentPaths().Select(path => (path, Read(path))));

    private static void ExtractDocument(string path, string text, List<VerifiedView> views, List<string> errors)
    {
        DocumentLine[] lines = SplitLines(text);
        int index = 0;
        while (index < lines.Length)
        {
            DocumentLine line = lines[index];
            Match fence = OpeningFence().Match(line.Text);
            if (fence.Success)
            {
                index = ExtractFence(path, lines, index, fence, views, errors);
                continue;
            }

            string trimmed = line.Text.Trim();
            Match tableMarker = TableMarker().Match(trimmed);
            if (tableMarker.Success)
            {
                DocumentLine[] table = [.. lines.Skip(index + 1).TakeWhile(tableLine => tableLine.Text.TrimStart() is ['|', ..])];
                if (table.Length == 0)
                {
                    errors.Add($"{path}:{line.Number}: table marker '{trimmed}' is not immediately followed by a table header row.");
                }

                views.Add(new VerifiedView(tableMarker.Groups["id"].Value, VerifiedViewKind.Table, path, line.Number, table));
                index += table.Length + 1;
                continue;
            }

            if (trimmed.StartsWith("<!-- verify", StringComparison.Ordinal))
            {
                errors.Add($"{path}:{line.Number}: malformed table marker '{trimmed}'; expected '<!-- verify: <view-id> -->'.");
            }

            index++;
        }
    }

    private static int ExtractFence(string path, DocumentLine[] lines, int openingIndex, Match fence, List<VerifiedView> views, List<string> errors)
    {
        int fenceLength = fence.Groups["fence"].Value.Length;
        string info = fence.Groups["info"].Value.Trim();
        int closingIndex = Array.FindIndex(lines, openingIndex + 1, line => IsClosingFence(line.Text, fenceLength));
        if (closingIndex < 0)
        {
            errors.Add($"{path}:{lines[openingIndex].Number}: fence is never closed.");
            closingIndex = lines.Length;
        }

        DocumentLine[] content = lines[(openingIndex + 1)..closingIndex];
        string firstContentLine = content.Length > 0 ? content[0].Text.Trim() : string.Empty;
        Match mermaidMarker = MermaidMarker().Match(firstContentLine);
        if (info == "mermaid" && mermaidMarker.Success)
        {
            views.Add(new VerifiedView(mermaidMarker.Groups["id"].Value, VerifiedViewKind.Mermaid, path, content[0].Number, content[1..]));
        }
        else if (info == "mermaid" && firstContentLine.StartsWith("%% verify", StringComparison.Ordinal))
        {
            errors.Add($"{path}:{content[0].Number}: malformed Mermaid marker '{firstContentLine}'; expected '%% verify: <view-id>'.");
        }

        return closingIndex + 1;
    }

    private static bool IsClosingFence(string line, int openingLength)
    {
        Match closing = ClosingFence().Match(line);
        return closing.Success && closing.Groups["fence"].Value.Length >= openingLength;
    }

    [GeneratedRegex("^ {0,3}(?<fence>`{3,})(?<info>[^`]*)$")]
    private static partial Regex OpeningFence();

    [GeneratedRegex("^ {0,3}(?<fence>`{3,})\\s*$")]
    private static partial Regex ClosingFence();

    [GeneratedRegex("^%% verify: " + IdPattern + "$")]
    private static partial Regex MermaidMarker();

    [GeneratedRegex("^<!-- verify: " + IdPattern + " -->$")]
    private static partial Regex TableMarker();
}
