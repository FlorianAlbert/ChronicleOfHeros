using ChronicleOfHeros.Architecture.Tests.CodeModels;

namespace ChronicleOfHeros.Architecture.Tests.VerifiedViews;

/// <summary>
/// The registered comparison for every verified-view id in docs/architecture/conventions.md § View catalogue.
/// </summary>
internal static class VerifiedViewCheckers
{
    /// <summary>The <c>project-references</c> view id.</summary>
    internal const string ProjectReferences = "project-references";

    /// <summary>The <c>aspire-resources</c> view id.</summary>
    internal const string AspireResources = "aspire-resources";

    /// <summary>The <c>aspire-parameters</c> view id.</summary>
    internal const string AspireParameters = "aspire-parameters";

    /// <summary>The <c>api-endpoints</c> view id.</summary>
    internal const string ApiEndpoints = "api-endpoints";

    /// <summary>The <c>capability-layers:&lt;capability&gt;</c> view id prefix.</summary>
    internal const string CapabilityLayersPrefix = "capability-layers:";

    /// <summary>The <c>dbcontext-entities:&lt;DbContext&gt;</c> view id prefix.</summary>
    internal const string DbContextEntitiesPrefix = "dbcontext-entities:";

    /// <summary>The <c>aspire-resources</c> columns.</summary>
    internal static readonly TableSchema AspireResourcesSchema = new(
        ["Resource", "Declared by", "Parent", "Project", "References", "Waits for", "Health check"],
        ["Resource"],
        ["References", "Waits for", "Health check"]);

    /// <summary>The <c>aspire-parameters</c> columns.</summary>
    internal static readonly TableSchema AspireParametersSchema = new(
        ["Parameter", "Secret", "Consumed as"],
        ["Parameter"],
        ["Consumed as"]);

    /// <summary>The <c>api-endpoints</c> columns.</summary>
    internal static readonly TableSchema ApiEndpointsSchema = new(
        ["Method", "Route", "Adapter", "Authorization"],
        ["Method", "Route"],
        ["Authorization"]);

    /// <summary>The <c>dbcontext-entities:&lt;DbContext&gt;</c> columns.</summary>
    internal static readonly TableSchema DbContextEntitiesSchema = new(["Entity", "Table"], ["Entity"], []);

    /// <summary>
    /// Gets every view id the code requires: the fixed ids, one per capability contracts project, and one per production <c>DbContext</c>.
    /// </summary>
    internal static IReadOnlyList<string> RequiredIds() =>
        [
            ProjectReferences,
            AspireResources,
            AspireParameters,
            ApiEndpoints,
            .. CapabilityLayerModel.Capabilities().Select(capability => CapabilityLayersPrefix + capability),
            .. DbContextModel.ProductionContexts().Select(context => DbContextEntitiesPrefix + context.Name),
        ];

    /// <summary>
    /// Compares a verified view with the code. Throws <see cref="FormatException"/> when the view violates the grammar.
    /// </summary>
    internal static ViewDifferences Check(VerifiedView view) => view.Id switch
    {
        ProjectReferences => CheckProjectReferences(view),
        AspireResources => CheckAspireResources(view),
        AspireParameters => CheckAspireParameters(view),
        ApiEndpoints => CheckApiEndpoints(view),
        string id when id.StartsWith(CapabilityLayersPrefix, StringComparison.Ordinal) =>
            CheckCapabilityLayers(view, id[CapabilityLayersPrefix.Length..]),
        string id when id.StartsWith(DbContextEntitiesPrefix, StringComparison.Ordinal) =>
            CheckDbContextEntities(view, id[DbContextEntitiesPrefix.Length..]),
        _ => throw new InvalidOperationException($"{view.Location}: no checker is registered for verified view '{view.Id}'."),
    };

    private static ViewDifferences CheckProjectReferences(VerifiedView view)
    {
        FlowchartView documented = FlowchartView.Parse(view);
        (IReadOnlyList<(string Source, string Target)> edges, IReadOnlyList<string> errors) = SolutionProjects.References();
        return new ViewDifferences()
            .CompareSets("project", documented.Nodes, SolutionProjects.All.Select(project => project.Name))
            .CompareSets("reference", documented.Edges.Select(edge => edge.ToString()), edges.Select(edge => new GraphEdge(edge.Source, edge.Target).ToString()))
            .AddCodeErrors(errors);
    }

    private static ViewDifferences CheckCapabilityLayers(VerifiedView view, string capability)
    {
        FlowchartView documented = FlowchartView.Parse(view);
        (IReadOnlyList<(string Source, string Target)> edges, IReadOnlyList<string> errors) =
            CapabilityLayerModel.Dependencies(capability, [.. documented.Nodes]);
        return new ViewDifferences()
            .CompareSets("dependency", documented.Edges.Select(edge => edge.ToString()), edges.Select(edge => new GraphEdge(edge.Source, edge.Target).ToString()))
            .AddCodeErrors(errors);
    }

    private static ViewDifferences CheckAspireResources(VerifiedView view)
    {
        AppHostModel model = AppHostModel.Load();
        IEnumerable<TableRow> actual = model.Resources.Select(resource => TableRows.Create(AspireResourcesSchema, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Resource"] = resource.Name,
            ["Declared by"] = resource.DeclaredBy,
            ["Parent"] = resource.Parent,
            ["Project"] = resource.Project,
            ["References"] = TableRows.ListCell(resource.References),
            ["Waits for"] = TableRows.ListCell(resource.WaitsFor),
            ["Health check"] = TableRows.ListCell(resource.HealthChecks),
        }));
        return new ViewDifferences()
            .CompareRows(AspireResourcesSchema, TableView.Parse(view, AspireResourcesSchema), actual)
            .AddCodeErrors(model.Errors);
    }

    private static ViewDifferences CheckAspireParameters(VerifiedView view)
    {
        AppHostModel model = AppHostModel.Load();
        IEnumerable<TableRow> actual = model.Parameters.Select(parameter => TableRows.Create(AspireParametersSchema, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Parameter"] = parameter.Name,
            ["Secret"] = parameter.Secret ? "yes" : "no",
            ["Consumed as"] = TableRows.ListCell(parameter.ConsumedAs),
        }));
        return new ViewDifferences()
            .CompareRows(AspireParametersSchema, TableView.Parse(view, AspireParametersSchema), actual)
            .AddCodeErrors(model.Errors);
    }

    private static ViewDifferences CheckApiEndpoints(VerifiedView view)
    {
        ApiEndpointModel model = ApiEndpointModel.Load();
        List<string> errors = [.. model.Errors];
        errors.AddRange(model.Endpoints
            .GroupBy(endpoint => $"{endpoint.Method} {endpoint.Route}", StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} is mapped more than once: {string.Join(", ", group.Select(endpoint => endpoint.Location))}."));
        IEnumerable<TableRow> actual = model.Endpoints
            .DistinctBy(endpoint => $"{endpoint.Method} {endpoint.Route}", StringComparer.Ordinal)
            .Select(endpoint => TableRows.Create(ApiEndpointsSchema, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Method"] = endpoint.Method,
                ["Route"] = endpoint.Route,
                ["Adapter"] = endpoint.Adapter,
                ["Authorization"] = TableRows.ListCell(endpoint.Authorization),
            }));
        return new ViewDifferences()
            .CompareRows(ApiEndpointsSchema, TableView.Parse(view, ApiEndpointsSchema), actual)
            .AddCodeErrors(errors);
    }

    private static ViewDifferences CheckDbContextEntities(VerifiedView view, string contextName)
    {
        Type? context = DbContextModel.ProductionContexts().SingleOrDefault(type => type.Name == contextName);
        IEnumerable<TableRow> actual = context is null
            ? []
            : DbContextModel.Entities(context).Select(entity => TableRows.Create(DbContextEntitiesSchema, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Entity"] = entity.Entity,
                ["Table"] = entity.Table,
            }));
        return new ViewDifferences()
            .CompareRows(DbContextEntitiesSchema, TableView.Parse(view, DbContextEntitiesSchema), actual)
            .AddCodeErrors(context is null ? [$"no production DbContext named '{contextName}' exists."] : []);
    }
}
