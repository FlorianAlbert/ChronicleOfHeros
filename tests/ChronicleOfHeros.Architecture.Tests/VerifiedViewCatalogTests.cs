using ChronicleOfHeros.Architecture.Tests.VerifiedViews;

namespace ChronicleOfHeros.Architecture.Tests;

/// <summary>
/// Verifies that the architecture documents contain exactly the verified views the code requires, each with a registered checker.
/// </summary>
public sealed class VerifiedViewCatalogTests
{
#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Verifies that every verify marker in the documents is well-formed and every fence is closed.
    /// </summary>
    [Fact]
    public void Verify_markers_are_well_formed()
    {
        Assert.Empty(ArchitectureDocuments.Repository.FormatErrors);
    }

    /// <summary>
    /// Verifies that no view id appears more than once.
    /// </summary>
    [Fact]
    public void Each_verified_view_id_appears_once()
    {
        string[] duplicates = [.. ArchitectureDocuments.Repository.Views
            .GroupBy(view => view.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(view => view.Location))}")];

        Assert.Empty(duplicates);
    }

    /// <summary>
    /// Verifies that every documented view id has a registered checker; an unknown id fails.
    /// </summary>
    [Fact]
    public void Every_documented_view_has_a_registered_checker()
    {
        IReadOnlyList<string> required = VerifiedViewCheckers.RequiredIds();

        string[] unknown = [.. ArchitectureDocuments.Repository.Views
            .Where(view => !required.Contains(view.Id, StringComparer.Ordinal))
            .Select(view => $"{view.Id} ({view.Location})")];

        Assert.Empty(unknown);
    }

    /// <summary>
    /// Verifies that every view the code requires is documented; a deleted view fails.
    /// </summary>
    [Fact]
    public void Every_required_view_is_documented()
    {
        string[] missing = [.. VerifiedViewCheckers.RequiredIds()
            .Where(id => !ArchitectureDocuments.Repository.Views.Any(view => view.Id == id))];

        Assert.Empty(missing);
    }

    /// <summary>
    /// Verifies that the required view ids include the fixed catalogue entries and one per capability and DbContext.
    /// </summary>
    [Fact]
    public void Required_views_cover_the_catalogue()
    {
        IReadOnlyList<string> required = VerifiedViewCheckers.RequiredIds();

        Assert.Contains(VerifiedViewCheckers.ProjectReferences, required);
        Assert.Contains(VerifiedViewCheckers.AspireResources, required);
        Assert.Contains(VerifiedViewCheckers.AspireParameters, required);
        Assert.Contains(VerifiedViewCheckers.ApiEndpoints, required);
        Assert.Contains(required, id => id.StartsWith(VerifiedViewCheckers.CapabilityLayersPrefix, StringComparison.Ordinal));
        Assert.Contains(required, id => id.StartsWith(VerifiedViewCheckers.DbContextEntitiesPrefix, StringComparison.Ordinal));
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores
}
