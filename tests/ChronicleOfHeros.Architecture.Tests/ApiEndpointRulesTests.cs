using System.Text.RegularExpressions;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;

using ChronicleOfHeros.Architecture.Tests.CodeModels;
using ChronicleOfHeros.Architecture.Tests.VerifiedViews;
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
    /// Verifies that each documented endpoint adapter is declared once and owns exactly one route mapping.
    /// </summary>
    [Fact]
    public void Identity_endpoint_operations_have_one_adapter_and_one_route_mapping()
    {
        string endpointDirectory = ArchitectureTestPaths.FromRoot("src", "ChronicleOfHeros.Api", "Authentication");
        string[] endpointSourceFiles = Directory.GetFiles(endpointDirectory, "*Endpoints.cs", SearchOption.TopDirectoryOnly);
        string endpointSource = string.Concat(endpointSourceFiles.Select(File.ReadAllText));
        string[] documentedAdapters = [.. TableView
            .Parse(ArchitectureDocuments.Repository.Single(VerifiedViewCheckers.ApiEndpoints), VerifiedViewCheckers.ApiEndpointsSchema)
            .Select(row => row.Cells["Adapter"])
            .Order(StringComparer.Ordinal)];
        string[] declaredAdapters = [.. Regex.Matches(endpointSource, @"internal sealed class (?<name>\w+Endpoint)\(")
            .Select(match => match.Groups["name"].Value)
            .Order(StringComparer.Ordinal)];
        string[] mappingAdapters = [.. ApiEndpointModel.Load().Endpoints
            .Select(endpoint => endpoint.Adapter)
            .Order(StringComparer.Ordinal)];

        Assert.NotEmpty(documentedAdapters);
        Assert.Equal(documentedAdapters.Distinct(StringComparer.Ordinal), documentedAdapters);
        Assert.Equal(documentedAdapters, declaredAdapters);
        Assert.Equal(documentedAdapters, mappingAdapters);
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