using System.Text;

namespace ChronicleOfHeros.Architecture.Tests.VerifiedViews;

/// <summary>
/// The differences between a verified view and the code, formatted per docs/architecture/conventions.md § Failure messages.
/// </summary>
internal sealed class ViewDifferences
{
    private readonly List<string> missingFromDoc = [];
    private readonly List<string> missingFromCode = [];
    private readonly List<string> differing = [];
    private readonly List<string> codeErrors = [];

    /// <summary>
    /// Gets a value indicating whether the view matches the code.
    /// </summary>
    internal bool IsEmpty => missingFromDoc.Count == 0 && missingFromCode.Count == 0 && differing.Count == 0 && codeErrors.Count == 0;

    /// <summary>
    /// Gets every difference as one line each, for assertions on specific items.
    /// </summary>
    internal IEnumerable<string> Items => missingFromDoc
        .Select(item => $"missing from the doc: {item}")
        .Concat(missingFromCode.Select(item => $"missing from the code: {item}"))
        .Concat(differing.Select(item => $"differs: {item}"))
        .Concat(codeErrors.Select(item => $"code not extractable: {item}"));

    /// <summary>
    /// Compares two sets of items; items only in code are missing from the doc and vice versa.
    /// </summary>
    internal ViewDifferences CompareSets(string kind, IEnumerable<string> documented, IEnumerable<string> actual)
    {
        HashSet<string> doc = new(documented, StringComparer.Ordinal);
        HashSet<string> code = new(actual, StringComparer.Ordinal);
        missingFromDoc.AddRange(code.Except(doc).Order(StringComparer.Ordinal).Select(item => $"{kind} {item}"));
        missingFromCode.AddRange(doc.Except(code).Order(StringComparer.Ordinal).Select(item => $"{kind} {item}"));
        return this;
    }

    /// <summary>
    /// Compares table rows by key, reporting missing and differing rows.
    /// </summary>
    internal ViewDifferences CompareRows(TableSchema schema, IEnumerable<TableRow> documented, IEnumerable<TableRow> actual)
    {
        Dictionary<string, TableRow> doc = documented.ToDictionary(row => row.Key, StringComparer.Ordinal);
        Dictionary<string, TableRow> code = actual.ToDictionary(row => row.Key, StringComparer.Ordinal);
        missingFromDoc.AddRange(code.Keys.Except(doc.Keys).Order(StringComparer.Ordinal).Select(key => $"row {TableRows.Describe(schema, code[key])}"));
        missingFromCode.AddRange(doc.Keys.Except(code.Keys).Order(StringComparer.Ordinal).Select(key => $"row {TableRows.Describe(schema, doc[key])}"));
        differing.AddRange(doc.Keys.Intersect(code.Keys)
            .Order(StringComparer.Ordinal)
            .SelectMany(key => schema.Columns
                .Where(column => doc[key].Cells[column] != code[key].Cells[column])
                .Select(column => $"row '{key}' column '{column}': doc '{doc[key].Cells[column]}', code '{code[key].Cells[column]}'")));
        return this;
    }

    /// <summary>
    /// Records code that the view specification cannot extract.
    /// </summary>
    internal ViewDifferences AddCodeErrors(IEnumerable<string> errors)
    {
        codeErrors.AddRange(errors);
        return this;
    }

    /// <summary>
    /// Formats the failure message for the given view.
    /// </summary>
    internal string ToFailureMessage(VerifiedView view)
    {
        StringBuilder message = new($"Verified view '{view.Id}' ({view.Location}) does not match the code.");
        AppendSection(message, "Missing from the doc (in code only)", missingFromDoc);
        AppendSection(message, "Missing from the code (in doc only)", missingFromCode);
        AppendSection(message, "Differing", differing);
        AppendSection(message, "Code the specification in docs/architecture/conventions.md cannot extract", codeErrors);
        return message.ToString();
    }

    private static void AppendSection(StringBuilder message, string title, List<string> items)
    {
        if (items.Count > 0)
        {
            _ = message.AppendLine().Append(title).Append(':');
            items.ForEach(item => message.AppendLine().Append("  - ").Append(item));
        }
    }
}
