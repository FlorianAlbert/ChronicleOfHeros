using System.Text.RegularExpressions;

namespace ChronicleOfHeros.Architecture.Tests.VerifiedViews;

/// <summary>
/// A directed edge between two node labels.
/// </summary>
internal sealed record GraphEdge(string Source, string Target)
{
    /// <inheritdoc />
    public override string ToString() => $"{Source} --> {Target}";
}

/// <summary>
/// A verified Mermaid flowchart reduced to node labels and labelled edges, per docs/architecture/conventions.md § Mermaid grammar.
/// </summary>
internal sealed partial record FlowchartView(IReadOnlySet<string> Nodes, IReadOnlySet<GraphEdge> Edges)
{
    /// <summary>
    /// Parses a verified Mermaid view; throws <see cref="FormatException"/> naming file and line on any grammar violation.
    /// </summary>
    internal static FlowchartView Parse(VerifiedView view)
    {
        if (view.Kind != VerifiedViewKind.Mermaid)
        {
            throw new FormatException($"{view.Location}: verified view '{view.Id}' must be a Mermaid flowchart.");
        }

        Dictionary<string, string> labelsById = new(StringComparer.Ordinal);
        HashSet<string> labels = new(StringComparer.Ordinal);
        List<(DocumentLine Line, string Source, string Target)> rawEdges = [];
        bool hasHeader = false;
        foreach (DocumentLine line in view.Body)
        {
            string text = line.Text.Trim();
            if (text.Length == 0 || text.StartsWith("%%", StringComparison.Ordinal) || DraftStyling().IsMatch(text))
            {
                continue;
            }

            if (Header().IsMatch(text))
            {
                hasHeader = !hasHeader && labelsById.Count == 0 && rawEdges.Count == 0
                    ? true
                    : throw Error(view, line, "the flowchart header must appear exactly once, before nodes and edges");
                continue;
            }

            if (!hasHeader)
            {
                throw Error(view, line, "expected 'flowchart <dir>' or 'graph <dir>' before nodes and edges");
            }

            Match node = Node().Match(text);
            if (node.Success)
            {
                string id = node.Groups["id"].Value;
                string label = node.Groups["label"].Value;
                if (!labelsById.TryAdd(id, label) || !labels.Add(label))
                {
                    throw Error(view, line, $"node id '{id}' or label '{label}' is declared twice");
                }

                continue;
            }

            Match edge = Edge().Match(text);
            if (edge.Success)
            {
                rawEdges.Add((line, edge.Groups["source"].Value, edge.Groups["target"].Value));
                continue;
            }

            throw Error(view, line, $"'{text}' is not a node, edge, comment, or draft styling line");
        }

        HashSet<GraphEdge> edges = [];
        foreach ((DocumentLine line, string source, string target) in rawEdges)
        {
            if (!labelsById.TryGetValue(source, out string? sourceLabel) || !labelsById.TryGetValue(target, out string? targetLabel))
            {
                throw Error(view, line, "every edge endpoint must be a declared node");
            }

            if (source == target || !edges.Add(new GraphEdge(sourceLabel, targetLabel)))
            {
                throw Error(view, line, "self-loops and duplicate edges are not allowed");
            }
        }

        return new FlowchartView(labels, edges);
    }

    private static FormatException Error(VerifiedView view, DocumentLine line, string reason) =>
        new($"{view.LocationOf(line)}: verified view '{view.Id}': {reason} (docs/architecture/conventions.md § Mermaid grammar).");

    [GeneratedRegex("^(flowchart|graph) (TB|TD|BT|LR|RL)$")]
    private static partial Regex Header();

    [GeneratedRegex("^(?<id>[A-Za-z][A-Za-z0-9_]*)\\[\"(?<label>[^\"]+)\"\\](:::[A-Za-z0-9_-]+)?$")]
    private static partial Regex Node();

    [GeneratedRegex("^(?<source>[A-Za-z][A-Za-z0-9_]*) --> (?<target>[A-Za-z][A-Za-z0-9_]*)$")]
    private static partial Regex Edge();

    [GeneratedRegex("^(classDef [A-Za-z0-9_-]+ \\S.*|class [A-Za-z][A-Za-z0-9_]*(,[A-Za-z][A-Za-z0-9_]*)* [A-Za-z0-9_-]+)$")]
    private static partial Regex DraftStyling();
}
