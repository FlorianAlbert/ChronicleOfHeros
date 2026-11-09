using ChronicleOfHeros.Architecture.Tests.CodeModels;
using ChronicleOfHeros.Architecture.Tests.VerifiedViews;

namespace ChronicleOfHeros.Architecture.Tests;

/// <summary>
/// Proves the doc-conformance checks detect drift by mutating in-memory copies of the real documents; the files stay untouched.
/// </summary>
public sealed class VerifiedViewMutationTests
{
#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Verifies that removing a documented project reference is reported as missing from the doc.
    /// </summary>
    [Fact]
    public void Removed_project_reference_edge_is_reported()
    {
        AssertRemovedEdgeIsReported(VerifiedViewCheckers.ProjectReferences, "reference");
    }

    /// <summary>
    /// Verifies that removing a documented layer dependency is reported as missing from the doc.
    /// </summary>
    [Fact]
    public void Removed_capability_layer_edge_is_reported()
    {
        AssertRemovedEdgeIsReported(VerifiedViewCheckers.CapabilityLayersPrefix + CapabilityLayerModel.Capabilities()[0], "dependency");
    }

    /// <summary>
    /// Verifies that removing a project node is reported and that a project node without a project is reported.
    /// </summary>
    [Fact]
    public void Renamed_project_node_is_reported_in_both_directions()
    {
        VerifiedView original = ArchitectureDocuments.Repository.Single(VerifiedViewCheckers.ProjectReferences);
        VerifiedView mutated = Mutate(original, lines => lines.Select(line => line.Text.Replace(
            "[\"ChronicleOfHeros.Web\"]", "[\"ChronicleOfHeros.Web.Client\"]", StringComparison.Ordinal)));

        string[] items = [.. VerifiedViewCheckers.Check(mutated).Items];

        Assert.Contains("missing from the doc: project ChronicleOfHeros.Web", items);
        Assert.Contains("missing from the code: project ChronicleOfHeros.Web.Client", items);
    }

    /// <summary>
    /// Verifies that a fake endpoint row is reported as missing from the code.
    /// </summary>
    [Fact]
    public void Fake_endpoint_row_is_reported()
    {
        AssertAddedRowIsReported(VerifiedViewCheckers.ApiEndpoints, "| DELETE | /fake | FakeEndpoint | None |");
    }

    /// <summary>
    /// Verifies that a fake Aspire resource row is reported as missing from the code.
    /// </summary>
    [Fact]
    public void Fake_aspire_resource_row_is_reported()
    {
        AssertAddedRowIsReported(VerifiedViewCheckers.AspireResources, "| fake | AddRedis | - | - | - | - | - |");
    }

    /// <summary>
    /// Verifies that a fake Aspire parameter row is reported as missing from the code.
    /// </summary>
    [Fact]
    public void Fake_aspire_parameter_row_is_reported()
    {
        AssertAddedRowIsReported(VerifiedViewCheckers.AspireParameters, "| fake-parameter | yes | api:Fake__Value |");
    }

    /// <summary>
    /// Verifies that a fake entity row is reported as missing from the code.
    /// </summary>
    [Fact]
    public void Fake_entity_row_is_reported()
    {
        AssertAddedRowIsReported(
            VerifiedViewCheckers.DbContextEntitiesPrefix + DbContextModel.ProductionContexts()[0].Name,
            "| `FakeEntity` | FakeEntities |");
    }

    /// <summary>
    /// Verifies that changing a documented cell is reported as a differing row.
    /// </summary>
    [Fact]
    public void Changed_parameter_secret_is_reported_as_differing()
    {
        VerifiedView original = ArchitectureDocuments.Repository.Single(VerifiedViewCheckers.AspireParameters);
        DocumentLine firstRow = original.Body[2];
        string[] cells = firstRow.Text.Split('|');
        cells[2] = cells[2].Contains("yes", StringComparison.Ordinal) ? " no " : " yes ";
        VerifiedView mutated = Mutate(original, lines => lines.Select(line => line.Number == firstRow.Number ? string.Join('|', cells) : line.Text));

        string message = VerifiedViewCheckers.Check(mutated).ToFailureMessage(mutated);

        Assert.Contains("column 'Secret'", message, StringComparison.Ordinal);
        Assert.Contains(original.Location, message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a deleted marker removes the view from the catalogue and that an unknown id is not a required view.
    /// </summary>
    [Fact]
    public void Deleted_and_unknown_markers_are_detected()
    {
        VerifiedView original = ArchitectureDocuments.Repository.Single(VerifiedViewCheckers.ApiEndpoints);
        string[] lines = [.. ArchitectureDocuments.SplitLines(ArchitectureDocuments.Read(original.File)).Select(line => line.Text)];

        VerifiedViewCatalog deleted = ArchitectureDocuments.Extract(
            [(original.File, string.Join('\n', lines.Where((line, index) => index + 1 != original.Line)))]);
        VerifiedViewCatalog renamed = ArchitectureDocuments.Extract(
            [(original.File, string.Join('\n', lines.Select((line, index) => index + 1 == original.Line ? "<!-- verify: api-routes -->" : line)))]);

        Assert.DoesNotContain(deleted.Views, view => view.Id == VerifiedViewCheckers.ApiEndpoints);
        Assert.Contains(renamed.Views, view => view.Id == "api-routes");
        Assert.DoesNotContain("api-routes", VerifiedViewCheckers.RequiredIds());
    }

    /// <summary>
    /// Verifies that a malformed marker and a non-grammar Mermaid line are rejected with file and line.
    /// </summary>
    [Fact]
    public void Malformed_marker_and_chained_edge_are_rejected()
    {
        const string Document = "<!-- verify:api-endpoints -->\n\n```mermaid\n%% verify: project-references\nflowchart LR\n    a[\"A\"]\n    b[\"B\"]\n    c[\"C\"]\n    a --> b --> c\n```\n";

        VerifiedViewCatalog catalog = ArchitectureDocuments.Extract([("docs/architecture/sample.md", Document)]);
        FormatException exception = Assert.Throws<FormatException>(() => FlowchartView.Parse(catalog.Single(VerifiedViewCheckers.ProjectReferences)));

        Assert.Equal(["docs/architecture/sample.md:1: malformed table marker '<!-- verify:api-endpoints -->'; expected '<!-- verify: <view-id> -->'."], catalog.FormatErrors);
        Assert.StartsWith("docs/architecture/sample.md:9:", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the AppHost extraction follows variables, parents, references, waits, health checks, and parameters.
    /// </summary>
    [Fact]
    public void AppHost_extraction_follows_the_specification()
    {
        const string Source = """
            var builder = DistributedApplication.CreateBuilder(args);
            // builder.AddRedis("commented-out");
            var db = builder.AddPostgres("pg").AddDatabase("db");
            var key = builder.AddParameter("key", secret: true);
            var worker = builder.AddDotnetProject("worker", @"..\Worker\Worker.csproj").WithReference(db).WaitFor(db);
            builder.AddDotnetProject("api", "../Api/Api.csproj")
                .WithEnvironment("Key", key)
                .WaitForCompletion(worker)
                .WithHttpHealthCheck();
            builder.AddContainer("bad", "image").WithArgs(key);
            """;

        AppHostModel model = AppHostModel.Parse(Source);

        Assert.Equal(["pg", "db", "worker", "api", "bad"], model.Resources.Select(resource => resource.Name));
        Assert.Equal("pg", model.Resources[1].Parent);
        Assert.Equal("Worker", model.Resources[2].Project);
        Assert.Equal(["db"], model.Resources[2].References);
        Assert.Equal(["worker (completion)"], model.Resources[3].WaitsFor);
        Assert.Equal(["http /"], model.Resources[3].HealthChecks);
        Assert.True(model.Parameters.Single().Secret);
        Assert.Equal(["api:Key"], model.Parameters.Single().ConsumedAs);
        Assert.Single(model.Errors, error => error.Contains("WithArgs", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that endpoint extraction resolves groups, adapters, and authorization.
    /// </summary>
    [Fact]
    public void Endpoint_extraction_follows_the_specification()
    {
        const string Source = """
            var group = app.MapGroup("/admin").RequireAuthorization("Operator");
            group.MapDelete("/items/{id}", () => Results.Ok());
            app.MapGet("/open", () => "/not-a-route");
            internal sealed class SampleEndpoint
            {
                internal static void Map(IEndpointRouteBuilder endpoints) =>
                    endpoints.MapPatch("/sample", () => Results.Ok()).RequireAuthorization().AllowAnonymous();
            }
            """;

        ApiEndpointModel model = ApiEndpointModel.Parse([("src/Sample/Program.cs", Source)]);

        Assert.Empty(model.Errors);
        Assert.Equal(
            ["DELETE /admin/items/{id} Program Policy:Operator", "GET /open Program None", "PATCH /sample SampleEndpoint Authenticated, Anonymous"],
            model.Endpoints.Select(endpoint => $"{endpoint.Method} {endpoint.Route} {endpoint.Adapter} {string.Join(", ", endpoint.Authorization)}"));
    }

    /// <summary>
    /// Verifies that constructs the specification does not cover fail with file and line instead of being ignored.
    /// </summary>
    [Fact]
    public void Unreadable_constructs_are_reported_with_location()
    {
        const string AppHostSource = """
            var builder = DistributedApplication.CreateBuilder(args);
            if (true) { builder.AddRedis("cache"); }
            builder.AddContainer("broken", "image"
            """;
        const string EndpointSource = """
            var route = "/dynamic";
            app.MapGet(route, () => Results.Ok());
            app.MapPost("/policy", () => Results.Ok()).RequireAuthorization(PolicyName);
            app?.MapPut("/conditional", () => Results.Ok());
            """;

        string[] appHostErrors = [.. AppHostModel.Parse(AppHostSource).Errors];
        string[] endpointErrors = [.. ApiEndpointModel.Parse([("src/Sample/Program.cs", EndpointSource)]).Errors];

        Assert.Contains(appHostErrors, error => error.StartsWith($"{AppHostModel.SourcePath}:2: statement is not a single invocation chain", StringComparison.Ordinal));
        Assert.Contains(appHostErrors, error => error.StartsWith($"{AppHostModel.SourcePath}:3: C# syntax error", StringComparison.Ordinal));
        Assert.Equal(
            [
                "src/Sample/Program.cs:2: MapGet needs a string-literal route as its first argument.",
                "src/Sample/Program.cs:3: RequireAuthorization arguments must be string-literal policy names.",
                "src/Sample/Program.cs: 3 endpoint mapping calls exist but only 2 are reachable as invocation chains.",
            ],
            endpointErrors);
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores

    private static void AssertRemovedEdgeIsReported(string viewId, string kind)
    {
        VerifiedView original = ArchitectureDocuments.Repository.Single(viewId);
        FlowchartView graph = FlowchartView.Parse(original);
        DocumentLine edgeLine = original.Body.First(line => line.Text.Contains("-->", StringComparison.Ordinal));
        VerifiedView mutated = Mutate(original, lines => lines.Where(line => line.Number != edgeLine.Number).Select(line => line.Text));
        GraphEdge removed = graph.Edges.Except(FlowchartView.Parse(mutated).Edges).Single();

        ViewDifferences differences = VerifiedViewCheckers.Check(mutated);

        Assert.Contains($"missing from the doc: {kind} {removed}", differences.Items);
        Assert.Contains(original.Location, differences.ToFailureMessage(mutated), StringComparison.Ordinal);
    }

    private static void AssertAddedRowIsReported(string viewId, string row)
    {
        VerifiedView original = ArchitectureDocuments.Repository.Single(viewId);
        int lastRow = original.Body[^1].Number;
        VerifiedView mutated = Mutate(original, lines => lines.SelectMany(line => line.Number == lastRow ? [line.Text, row] : new[] { line.Text }));

        string[] items = [.. VerifiedViewCheckers.Check(mutated).Items];

        Assert.Contains($"missing from the code: row {row.Replace("`", string.Empty, StringComparison.Ordinal)}", items);
    }

    private static VerifiedView Mutate(VerifiedView original, Func<IEnumerable<DocumentLine>, IEnumerable<string>> mutation)
    {
        string text = string.Join('\n', mutation(ArchitectureDocuments.SplitLines(ArchitectureDocuments.Read(original.File))));
        return ArchitectureDocuments.Extract([(original.File, text)]).Single(original.Id);
    }
}
