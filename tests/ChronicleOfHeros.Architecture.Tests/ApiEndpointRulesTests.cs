using System.Text.RegularExpressions;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;

using ChronicleOfHeros.Identity.AspNetCore;
using ChronicleOfHeros.Identity.Contracts;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ChronicleOfHeros.Architecture.Tests;

/// <summary>
/// Verifies the API's explicit, contracts-only Identity endpoint adapters.
/// </summary>
public sealed class ApiEndpointRulesTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader().LoadAssemblies(
        System.Reflection.Assembly.Load("ChronicleOfHeros.Api"),
        typeof(IAuthenticationService).Assembly,
        typeof(AspNetCoreIdentityRegistration).Assembly).Build();

#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Verifies that endpoint adapters do not depend on Identity implementation types.
    /// </summary>
    [Fact]
    public void Identity_endpoint_adapters_depend_on_contracts_not_implementation()
    {
        IObjectProvider<IType> endpoints = Types()
            .That()
            .ResideInNamespace("ChronicleOfHeros.Api.Authentication");
        IObjectProvider<IType> implementationTypes = Types()
            .That()
            .ResideInAssembly(typeof(AspNetCoreIdentityRegistration).Assembly.GetName().Name);

        IArchRule endpointsMustNotDependOnImplementation = Types().That().Are(endpoints).Should().NotDependOnAny(implementationTypes)
            .Because("HTTP endpoint adapters use only Identity contracts")
            .WithoutRequiringPositiveResults();

        endpointsMustNotDependOnImplementation.Check(Architecture);
    }

    /// <summary>
    /// Verifies that each Identity HTTP operation has one explicit endpoint adapter and route mapping.
    /// </summary>
    [Fact]
    public void Identity_endpoint_operations_have_one_adapter_and_one_route_mapping()
    {
        string endpointDirectory = ArchitectureTestPaths.FromRoot("src", "ChronicleOfHeros.Api", "Authentication");
        string[] endpointSourceFiles = Directory.GetFiles(endpointDirectory, "*Endpoints.cs", SearchOption.TopDirectoryOnly);
        string endpointSource = string.Concat(endpointSourceFiles.Select(File.ReadAllText));

        Assert.Equal(7, Regex.Count(endpointSource, @"internal sealed class \w+Endpoint\("));
        Assert.Equal(7, Regex.Count(endpointSource, @"\.Map(?:Get|Post)\("));
        Assert.All(endpointSourceFiles, sourceFile =>
        {
            string source = File.ReadAllText(sourceFile);
            Assert.Contains("using ChronicleOfHeros.Identity.Contracts;", source, StringComparison.Ordinal);
            Assert.DoesNotContain("using ChronicleOfHeros.Identity.AspNetCore;", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Assembly.Get", source, StringComparison.Ordinal);
        });
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores
}