using ChronicleOfHeros.Identity.AspNetCore;

namespace ChronicleOfHeros.Architecture.Tests;

/// <summary>
/// Verifies the public surface exposed by the Identity implementation assembly.
/// </summary>
public sealed class IdentityPublicSurfaceTests
{
    private static readonly string[] FrameworkSurfaceExceptionTypeNames = [];

#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Verifies that only the registration API and its configuration options are public.
    /// </summary>
    [Fact]
    public void Identity_implementation_exposes_only_its_registration_surface()
    {
        string[] publicTypeNames = [.. typeof(AspNetCoreIdentityRegistration).Assembly
            .GetExportedTypes()
            .Where(type => !type.IsNested)
            .Select(type => type.FullName!)
            .OrderBy(typeName => typeName)];

        string[] allowedPublicTypeNames =
        [
                typeof(AspNetCoreIdentityRegistration).FullName!,
                typeof(AspNetCoreIdentityRegistrationOptions).FullName!,
            .. FrameworkSurfaceExceptionTypeNames,
        ];

        Assert.Equal(allowedPublicTypeNames.OrderBy(typeName => typeName), publicTypeNames);
    }

    /// <summary>
    /// Verifies that no framework-surface exception is currently allowed without a supporting ADR.
    /// </summary>
    [Fact]
    public void Identity_has_no_unapproved_framework_surface_exceptions()
    {
        Assert.Empty(FrameworkSurfaceExceptionTypeNames);
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores
}