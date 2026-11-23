using ChronicleOfHeros.Architecture.Tests.CodeModels;
using ChronicleOfHeros.Architecture.Tests.VerifiedViews;

namespace ChronicleOfHeros.Architecture.Tests;

/// <summary>
/// Compares every verified view in the architecture documents with the code, in both directions.
/// </summary>
public sealed class VerifiedViewConformanceTests
{
#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Verifies that each <c>capability-layers</c> diagram matches the compiled namespace dependencies of its capability.
    /// </summary>
    [Fact]
    public void Capability_layers_views_match_compiled_dependencies()
    {
        foreach (string capability in CapabilityLayerModel.Capabilities())
        {
            AssertMatches(VerifiedViewCheckers.CapabilityLayersPrefix + capability);
        }
    }

    /// <summary>
    /// Verifies that the <c>project-references</c> diagram matches the solution's projects and project references.
    /// </summary>
    [Fact]
    public void Project_references_view_matches_the_solution() => AssertMatches(VerifiedViewCheckers.ProjectReferences);

    /// <summary>
    /// Verifies that the <c>aspire-resources</c> table matches the AppHost's declared resources.
    /// </summary>
    [Fact]
    public void Aspire_resources_view_matches_the_AppHost() => AssertMatches(VerifiedViewCheckers.AspireResources);

    /// <summary>
    /// Verifies that the <c>aspire-parameters</c> table matches the AppHost's declared parameters.
    /// </summary>
    [Fact]
    public void Aspire_parameters_view_matches_the_AppHost() => AssertMatches(VerifiedViewCheckers.AspireParameters);

    /// <summary>
    /// Verifies that the <c>api-endpoints</c> table matches the API's mapped endpoints.
    /// </summary>
    [Fact]
    public void Api_endpoints_view_matches_the_mapped_endpoints() => AssertMatches(VerifiedViewCheckers.ApiEndpoints);

#pragma warning restore CA1707 // Identifiers should not contain underscores

    private static void AssertMatches(string viewId)
    {
        VerifiedView view = ArchitectureDocuments.Repository.Single(viewId);
        ViewDifferences differences = VerifiedViewCheckers.Check(view);

        Assert.True(differences.IsEmpty, differences.ToFailureMessage(view));
    }
}
