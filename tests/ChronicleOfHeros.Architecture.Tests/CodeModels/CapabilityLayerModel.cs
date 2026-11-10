using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

namespace ChronicleOfHeros.Architecture.Tests.CodeModels;

/// <summary>
/// Computes namespace-level dependencies of a capability's assemblies per docs/architecture/conventions.md § <c>capability-layers:&lt;capability&gt;</c>.
/// </summary>
internal static class CapabilityLayerModel
{
    private const string ProjectPrefix = "ChronicleOfHeros.";
    private const string ContractsSuffix = ".Contracts";

    /// <summary>
    /// Gets the capability names, in lower case, of every solution project named <c>ChronicleOfHeros.&lt;Name&gt;.Contracts</c>.
    /// </summary>
    internal static IReadOnlyList<string> Capabilities() =>
        [.. SolutionProjects.All
            .Select(project => project.Name)
            .Where(name => name.StartsWith(ProjectPrefix, StringComparison.Ordinal) && name.EndsWith(ContractsSuffix, StringComparison.Ordinal))
            .Select(name => name[ProjectPrefix.Length..^ContractsSuffix.Length])
            .Where(name => !name.Contains('.', StringComparison.Ordinal))
#pragma warning disable CA1308 // View ids are lower case by specification (docs/architecture/conventions.md).
            .Select(name => name.ToLowerInvariant())
#pragma warning restore CA1308
            .Order(StringComparer.Ordinal)];

    /// <summary>
    /// Gets the non-test solution projects of a capability.
    /// </summary>
    internal static IReadOnlyList<SolutionProject> ScopeProjects(string capability) =>
        [.. SolutionProjects.Production
            .Where(project => project.Name.StartsWith(ProjectPrefix, StringComparison.Ordinal))
            .Where(project => project.Name[ProjectPrefix.Length..].Split('.')[0].Equals(capability, StringComparison.OrdinalIgnoreCase))
            .Where(project => project.Name.Split('.').Length > 2)];

    /// <summary>
    /// Maps the capability's types to the given namespace nodes and returns the edges between nodes and any errors.
    /// </summary>
    internal static (IReadOnlyList<(string Source, string Target)> Edges, IReadOnlyList<string> Errors) Dependencies(
        string capability,
        IReadOnlyCollection<string> nodes)
    {
        IReadOnlyList<SolutionProject> projects = ScopeProjects(capability);
        HashSet<string> assemblyNames = new(projects.Select(project => project.Name), StringComparer.Ordinal);
        ArchUnitNET.Domain.Architecture architecture = new ArchLoader()
            .LoadAssemblies([.. projects.Select(project => project.LoadAssembly())])
            .Build();
        IType[] scopeTypes = [.. architecture.Types.Where(type => assemblyNames.Contains(type.Assembly.Name))];

        List<string> errors = [];
        HashSet<string> occupiedNodes = new(StringComparer.Ordinal);
        HashSet<(string Source, string Target)> edges = [];
        foreach (IType type in scopeTypes)
        {
            if (IsCompilerGenerated(type) && !type.IsNested)
            {
                continue;
            }

            string? source = NodeOf(type, nodes);
            if (source is null)
            {
                errors.Add($"type '{type.FullName}' in namespace '{type.Namespace.FullName}' belongs to no node.");
                continue;
            }

            _ = occupiedNodes.Add(source);
            IEnumerable<string> targets = type.Dependencies
                .Select(dependency => dependency.Target)
                .Where(target => assemblyNames.Contains(target.Assembly.Name))
                .Where(target => !(IsCompilerGenerated(target) && !target.IsNested))
                .Select(target => NodeOf(target, nodes))
                .OfType<string>()
                .Where(target => target != source);
            edges.UnionWith(targets.Select(target => (source, target)));
        }

        errors.AddRange(nodes.Except(occupiedNodes, StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(node => $"node '{node}' contains no type."));
        return ([.. edges], errors);
    }

    /// <summary>
    /// Returns the node whose label is the longest namespace prefix, on <c>.</c> boundaries, of the type's namespace.
    /// </summary>
    internal static string? NodeOf(IType type, IReadOnlyCollection<string> nodes)
    {
        string typeNamespace = type.Namespace.FullName;
        return nodes
            .Where(node => typeNamespace == node || typeNamespace.StartsWith(node + ".", StringComparison.Ordinal))
            .OrderByDescending(node => node.Length)
            .FirstOrDefault();
    }

    // Nested compiler-generated types (closures, state machines) share their declaring type's namespace, so they count for it.
    private static bool IsCompilerGenerated(IType type) =>
        type.IsCompilerGenerated
            || type.Name.Contains('<', StringComparison.Ordinal)
            || type.AttributeInstances.Any(attribute => attribute.Type.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
}
