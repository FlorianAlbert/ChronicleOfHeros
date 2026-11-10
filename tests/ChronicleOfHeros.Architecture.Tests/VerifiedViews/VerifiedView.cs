namespace ChronicleOfHeros.Architecture.Tests.VerifiedViews;

/// <summary>
/// The notation of a verified view.
/// </summary>
internal enum VerifiedViewKind
{
    Mermaid,
    Table,
}

/// <summary>
/// One numbered line of an architecture document, without its line terminator.
/// </summary>
internal sealed record DocumentLine(int Number, string Text);

/// <summary>
/// A verified diagram or table extracted from an architecture document.
/// </summary>
/// <param name="Id">The view id from the marker.</param>
/// <param name="Kind">The view notation.</param>
/// <param name="File">The repository-relative document path with <c>/</c> separators.</param>
/// <param name="Line">The 1-based line of the marker.</param>
/// <param name="Body">For Mermaid, the lines after the marker up to the closing fence; for tables, the table lines.</param>
internal sealed record VerifiedView(string Id, VerifiedViewKind Kind, string File, int Line, IReadOnlyList<DocumentLine> Body)
{
    /// <summary>
    /// Gets the marker location as <c>file:line</c>.
    /// </summary>
    internal string Location => $"{File}:{Line}";

    /// <summary>
    /// Gets the location of a body line as <c>file:line</c>.
    /// </summary>
    internal string LocationOf(DocumentLine line) => $"{File}:{line.Number}";
}
