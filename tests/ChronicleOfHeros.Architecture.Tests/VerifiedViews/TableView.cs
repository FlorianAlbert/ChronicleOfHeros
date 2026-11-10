using System.Text.RegularExpressions;

namespace ChronicleOfHeros.Architecture.Tests.VerifiedViews;

/// <summary>
/// The columns of a verified table view.
/// </summary>
/// <param name="Columns">The exact header cells, in order.</param>
/// <param name="KeyColumns">The columns whose values identify a row.</param>
/// <param name="ListColumns">The columns holding <c>, </c>-separated sets, where <c>-</c> means none.</param>
internal sealed record TableSchema(IReadOnlyList<string> Columns, IReadOnlyList<string> KeyColumns, IReadOnlyList<string> ListColumns);

/// <summary>
/// One table row with canonical cell values: list cells are sorted, <c>, </c>-joined, and <c>-</c> when empty.
/// </summary>
internal sealed record TableRow(string Key, IReadOnlyDictionary<string, string> Cells);

/// <summary>
/// Builds canonical rows so that documented and extracted rows compare as text.
/// </summary>
internal static class TableRows
{
    /// <summary>
    /// Formats a set as a canonical list cell.
    /// </summary>
    internal static string ListCell(IEnumerable<string> items)
    {
        string[] sorted = [.. items.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        return sorted.Length == 0 ? "-" : string.Join(", ", sorted);
    }

    /// <summary>
    /// Creates a row from cells already in canonical form.
    /// </summary>
    internal static TableRow Create(TableSchema schema, IReadOnlyDictionary<string, string> cells) =>
        new(string.Join(" ", schema.KeyColumns.Select(column => cells[column])), cells);

    /// <summary>
    /// Formats a row for failure messages.
    /// </summary>
    internal static string Describe(TableSchema schema, TableRow row) =>
        "| " + string.Join(" | ", schema.Columns.Select(column => row.Cells[column])) + " |";
}

/// <summary>
/// Parses verified Markdown tables per docs/architecture/conventions.md § Table grammar.
/// </summary>
internal static partial class TableView
{
    private static readonly string[] DraftPrefixes = ["(added) ", "(removed) ", "(changed) "];

    /// <summary>
    /// Parses a verified table view; throws <see cref="FormatException"/> naming file and line on any grammar violation.
    /// </summary>
    internal static IReadOnlyList<TableRow> Parse(VerifiedView view, TableSchema schema)
    {
        if (view.Kind != VerifiedViewKind.Table)
        {
            throw new FormatException($"{view.Location}: verified view '{view.Id}' must be a Markdown table.");
        }

        if (view.Body.Count < 2)
        {
            throw Error(view, view.Line, "expected a header row and a delimiter row after the marker");
        }

        string[] header = SplitRow(view, view.Body[0]);
        if (!header.SequenceEqual(schema.Columns, StringComparer.Ordinal))
        {
            throw Error(view, view.Body[0].Number, $"expected header '| {string.Join(" | ", schema.Columns)} |'");
        }

        string[] delimiter = SplitRow(view, view.Body[1]);
        if (delimiter.Length != schema.Columns.Count || !delimiter.All(cell => DelimiterCell().IsMatch(cell)))
        {
            throw Error(view, view.Body[1].Number, "expected a delimiter row such as '|---|---|'");
        }

        List<TableRow> rows = [];
        foreach (DocumentLine line in view.Body.Skip(2))
        {
            string[] cells = SplitRow(view, line);
            if (cells.Length != schema.Columns.Count)
            {
                throw Error(view, line.Number, $"expected {schema.Columns.Count} cells, found {cells.Length}");
            }

            string? prefix = DraftPrefixes.FirstOrDefault(draftPrefix => cells[0].StartsWith(draftPrefix, StringComparison.Ordinal));
            cells[0] = prefix is null ? cells[0] : cells[0][prefix.Length..];

            Dictionary<string, string> canonical = schema.Columns
                .Select((column, index) => (column, value: schema.ListColumns.Contains(column) ? ParseList(view, line, cells[index]) : cells[index]))
                .ToDictionary(cell => cell.column, cell => cell.value, StringComparer.Ordinal);
            TableRow row = TableRows.Create(schema, canonical);
            if (rows.Any(existing => existing.Key == row.Key))
            {
                throw Error(view, line.Number, $"duplicate key '{row.Key}'");
            }

            rows.Add(row);
        }

        return rows;
    }

    private static string ParseList(VerifiedView view, DocumentLine line, string cell)
    {
        if (cell == "-")
        {
            return "-";
        }

        string[] items = cell.Split(", ");
        return items.Any(item => item.Length == 0 || item == "-" || item != item.Trim())
            ? throw Error(view, line.Number, $"list cell '{cell}' must separate non-empty items with ', '")
            : items.Distinct(StringComparer.Ordinal).Count() != items.Length
                ? throw Error(view, line.Number, $"list cell '{cell}' contains a duplicate item")
                : TableRows.ListCell(items);
    }

    private static string[] SplitRow(VerifiedView view, DocumentLine line)
    {
        string text = line.Text.Trim();
        return text is not ['|', _, ..] || text[^1] != '|'
            ? throw Error(view, line.Number, "a table row must start and end with '|'")
            : [.. text[1..^1].Split('|').Select(cell => cell.Replace("`", string.Empty, StringComparison.Ordinal).Trim())];
    }

    private static FormatException Error(VerifiedView view, int line, string reason) =>
        new($"{view.File}:{line}: verified view '{view.Id}': {reason} (docs/architecture/conventions.md § Table grammar).");

    [GeneratedRegex("^:?-{3,}:?$")]
    private static partial Regex DelimiterCell();
}
