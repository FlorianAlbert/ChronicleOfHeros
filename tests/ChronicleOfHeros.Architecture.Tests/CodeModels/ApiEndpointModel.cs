using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ChronicleOfHeros.Architecture.Tests.CodeModels;

/// <summary>
/// An HTTP endpoint mapping extracted from the API's source.
/// </summary>
internal sealed record ApiEndpoint(string Method, string Route, string Adapter, IReadOnlyList<string> Authorization, string Location);

/// <summary>
/// Extracts endpoint mappings from the syntax of <c>src/ChronicleOfHeros.Api/**/*.cs</c>
/// per docs/architecture/conventions.md § <c>api-endpoints</c>.
/// </summary>
internal sealed class ApiEndpointModel
{
    /// <summary>The repository-relative path of the API project directory.</summary>
    internal const string ProjectDirectory = "src/ChronicleOfHeros.Api";

    private static readonly string[] Verbs = ["MapGet", "MapPost", "MapPut", "MapDelete", "MapPatch"];

    private ApiEndpointModel()
    {
    }

    /// <summary>Gets the mapped endpoints.</summary>
    internal List<ApiEndpoint> Endpoints { get; } = [];

    /// <summary>Gets the constructs the specification does not cover, with file and line.</summary>
    internal List<string> Errors { get; } = [];

    /// <summary>
    /// Extracts the model from the API project's source files, excluding <c>bin</c> and <c>obj</c>.
    /// </summary>
    internal static ApiEndpointModel Load()
    {
        string directory = ArchitectureTestPaths.FromRoot([.. ProjectDirectory.Split('/')]);
        return Parse(Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Select(ArchitectureTestPaths.ToRepositoryPath)
            .Where(path => !path[(ProjectDirectory.Length + 1)..].Split('/').Any(segment => segment is "bin" or "obj"))
            .Order(StringComparer.Ordinal)
            .Select(path => (path, File.ReadAllText(ArchitectureTestPaths.FromRoot([.. path.Split('/')])))));
    }

    /// <summary>
    /// Extracts the model from source files given as pairs of repository-relative path and text.
    /// </summary>
    internal static ApiEndpointModel Parse(IEnumerable<(string Path, string Source)> files)
    {
        ApiEndpointModel model = new();
        foreach ((string path, string source) in files)
        {
            model.ReadFile(path, CSharpSource.Parse(path, source, model.Errors));
        }

        return model;
    }

    private void ReadFile(string path, CompilationUnitSyntax root)
    {
        Dictionary<string, InvocationChain> groupVariables = ReadGroupVariables(root);
        int expected = root
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Count(invocation => InvokedName(invocation) is string name && Verbs.Contains(name));
        int found = 0;
        foreach (InvocationChain chain in root
            .DescendantNodes()
            .OfType<ExpressionSyntax>()
            .Where(IsChainHead)
            .Select(InvocationChain.Read)
            .OfType<InvocationChain>())
        {
            for (int memberIndex = 0; memberIndex < chain.Members.Count; memberIndex++)
            {
                ChainMember member = chain.Members[memberIndex];
                if (!member.IsCall || !Verbs.Contains(member.Name))
                {
                    continue;
                }

                found++;
                string location = $"{path}:{member.Line}";
                string? route = member.Arguments is [{ StringValue: string literal }, ..] ? literal : null;
                if (route is null)
                {
                    Errors.Add($"{location}: {member.Name} needs a string-literal route as its first argument.");
                    continue;
                }

                (string prefix, List<string> authorization) = ReadGroups(chain, memberIndex, groupVariables, location, 0);
                authorization.AddRange(ReadAuthorization(chain.Members.Skip(memberIndex + 1).TakeWhile(next => !Verbs.Contains(next.Name)), location));
                string adapter = member.Node
                    .Ancestors()
                    .OfType<TypeDeclarationSyntax>()
                    .Select(type => type.Identifier.ValueText)
                    .FirstOrDefault("Program");
                Endpoints.Add(new ApiEndpoint(
                    member.Name[3..].ToUpperInvariant(),
                    JoinRoute(prefix, route),
                    adapter,
                    authorization.Count == 0 ? ["None"] : authorization,
                    location));
            }
        }

        if (found != expected)
        {
            Errors.Add($"{path}: {expected} endpoint mapping calls exist but only {found} are reachable as invocation chains.");
        }
    }

    private (string Prefix, List<string> Authorization) ReadGroups(
        InvocationChain chain,
        int verbIndex,
        Dictionary<string, InvocationChain> groupVariables,
        string location,
        int depth)
    {
        (string prefix, List<string> authorization) = depth < 8 && groupVariables.TryGetValue(chain.Root, out InvocationChain? group)
            ? ReadGroups(group, group.Members.Count, groupVariables, location, depth + 1)
            : (string.Empty, []);
        bool inGroup = false;
        foreach (ChainMember member in chain.Members.Take(verbIndex))
        {
            if (member is { Name: "MapGroup", IsCall: true })
            {
                inGroup = true;
                string? groupRoute = member.Arguments is [{ StringValue: string literal }, ..] ? literal : null;
                if (groupRoute is null)
                {
                    Errors.Add($"{location}: MapGroup needs a string-literal prefix as its first argument.");
                }

                prefix = JoinRoute(prefix, groupRoute ?? string.Empty);
            }
            else if (inGroup)
            {
                authorization.AddRange(ReadAuthorization([member], location));
            }
        }

        return (prefix, authorization);
    }

    private List<string> ReadAuthorization(IEnumerable<ChainMember> members, string location)
    {
        List<string> authorization = [];
        foreach (ChainMember member in members.Where(member => member.IsCall))
        {
            if (member.Name == "AllowAnonymous")
            {
                authorization.Add("Anonymous");
            }
            else if (member.Name == "RequireAuthorization" && member.Arguments!.Count == 0)
            {
                authorization.Add("Authenticated");
            }
            else if (member.Name == "RequireAuthorization")
            {
                foreach (CallArgument argument in member.Arguments!)
                {
                    if (argument.StringValue is string policy)
                    {
                        authorization.Add($"Policy:{policy}");
                    }
                    else
                    {
                        Errors.Add($"{location}: RequireAuthorization arguments must be string-literal policy names.");
                    }
                }
            }
        }

        return authorization;
    }

    private static string JoinRoute(string prefix, string route) =>
        prefix is [.., '/'] && route is ['/', ..] ? prefix + route[1..] : prefix + route;

    private static string? InvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        SimpleNameSyntax name => name.Identifier.ValueText,
        _ => null,
    };

    private static bool IsChainHead(ExpressionSyntax expression) =>
        expression is InvocationExpressionSyntax or MemberAccessExpressionSyntax
        && expression.Parent switch
        {
            MemberAccessExpressionSyntax access => access.Expression != expression,
            InvocationExpressionSyntax invocation => invocation.Expression != expression,
            _ => true,
        };

    private static Dictionary<string, InvocationChain> ReadGroupVariables(CompilationUnitSyntax root) =>
        root
            .DescendantNodes()
            .Select(node => node switch
            {
                VariableDeclaratorSyntax { Initializer.Value: var value } declarator => (name: declarator.Identifier.ValueText, value),
                AssignmentExpressionSyntax { Left: IdentifierNameSyntax target } assignment
                    when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) => (name: target.Identifier.ValueText, value: assignment.Right),
                _ => (name: string.Empty, value: (ExpressionSyntax?)null),
            })
            .Where(pair => pair.value is not null)
            .Select(pair => (pair.name, chain: InvocationChain.Read(pair.value!)))
            .Where(pair => pair.chain is not null && pair.chain.Members.Any(member => member.Name == "MapGroup"))
            .GroupBy(pair => pair.name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().chain!, StringComparer.Ordinal);
}
