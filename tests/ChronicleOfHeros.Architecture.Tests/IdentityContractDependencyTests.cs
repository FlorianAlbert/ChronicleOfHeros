using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;

using ChronicleOfHeros.Identity.AspNetCore;
using ChronicleOfHeros.Identity.Contracts;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ChronicleOfHeros.Architecture.Tests;

/// <summary>
/// Verifies the Identity contract and implementation assembly boundary.
/// </summary>
public sealed class IdentityContractDependencyTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader().LoadAssemblies(
        typeof(IAuthenticationService).Assembly,
        typeof(AspNetCoreIdentityRegistration).Assembly).Build();

#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Verifies that contract types do not depend on Identity implementation types.
    /// </summary>
    [Fact]
    public void Identity_contracts_do_not_depend_on_implementation()
    {
        IObjectProvider<IType> contractTypes = Types()
            .That()
            .ResideInAssembly(typeof(IAuthenticationService).Assembly.GetName().Name);
        IObjectProvider<IType> implementationTypes = Types()
            .That()
            .ResideInAssembly(typeof(AspNetCoreIdentityRegistration).Assembly.GetName().Name);

        IArchRule contractsMustNotDependOnImplementation = Types().That().Are(contractTypes).Should().NotDependOnAny(implementationTypes)
            .Because("contracts are the capability's consumer-facing boundary")
            .WithoutRequiringPositiveResults();

        contractsMustNotDependOnImplementation.Check(Architecture);
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores
}