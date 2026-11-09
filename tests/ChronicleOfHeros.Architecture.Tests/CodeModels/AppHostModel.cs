using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ChronicleOfHeros.Architecture.Tests.CodeModels;

/// <summary>
/// An Aspire resource extracted from <c>AppHost.cs</c>.
/// </summary>
internal sealed record AspireResource(string Name, string DeclaredBy, string Parent, string Project)
{
    /// <summary>Gets the names of referenced resources.</summary>
    internal List<string> References { get; } = [];

    /// <summary>Gets the awaited resources, suffixed with <c> (completion)</c> for <c>WaitForCompletion</c>.</summary>
    internal List<string> WaitsFor { get; } = [];

    /// <summary>Gets the health checks as <c>http &lt;path&gt;</c>.</summary>
    internal List<string> HealthChecks { get; } = [];
}

/// <summary>
/// An Aspire parameter extracted from <c>AppHost.cs</c>.
/// </summary>
internal sealed record AspireParameter(string Name, bool Secret)
{
    /// <summary>Gets the consumers as <c>&lt;resource&gt;:&lt;VARIABLE&gt;</c>.</summary>
    internal List<string> ConsumedAs { get; } = [];
}

/// <summary>
/// Extracts the resource graph from the syntax of <c>src/ChronicleOfHeros.AppHost/AppHost.cs</c>
/// per docs/architecture/conventions.md § <c>aspire-resources</c> and § <c>aspire-parameters</c>.
/// </summary>
internal sealed class AppHostModel
{
    /// <summary>The repository-relative path of the AppHost entry point.</summary>
    internal const string SourcePath = "src/ChronicleOfHeros.AppHost/AppHost.cs";

    private readonly Dictionary<string, object> symbols = new(StringComparer.Ordinal);
    private AppHostModel()
    {
    }

    /// <summary>Gets the declared resources in declaration order.</summary>
    internal List<AspireResource> Resources { get; } = [];

    /// <summary>Gets the declared parameters in declaration order.</summary>
    internal List<AspireParameter> Parameters { get; } = [];

    /// <summary>Gets the constructs the specification does not cover, with file and line.</summary>
    internal List<string> Errors { get; } = [];

    /// <summary>
    /// Extracts the model from the repository's AppHost source.
    /// </summary>
    internal static AppHostModel Load() => Parse(File.ReadAllText(ArchitectureTestPaths.FromRoot([.. SourcePath.Split('/')])));

    /// <summary>
    /// Extracts the model from AppHost source text.
    /// </summary>
    internal static AppHostModel Parse(string source)
    {
        AppHostModel model = new();
        CompilationUnitSyntax root = CSharpSource.Parse(SourcePath, source, model.Errors);
        foreach (MemberDeclarationSyntax member in root.Members)
        {
            model.ReadMember(member);
        }

        return model;
    }

    private void ReadMember(MemberDeclarationSyntax member)
    {
        (string? variable, ExpressionSyntax? value) = member is GlobalStatementSyntax { Statement: var statement }
            ? ReadStatement(statement)
            : (null, null);
        InvocationChain? chain = value is null ? null : InvocationChain.Read(value);
        if (chain is null)
        {
            if (member.DescendantTokens().Any(token => token.IsKind(SyntaxKind.IdentifierToken) && IsModelCall(token.ValueText)))
            {
                Errors.Add($"{SourcePath}:{member.Line}: statement is not a single invocation chain, so its resource calls cannot be read.");
            }

            return;
        }

        object? symbol = ReadChain(chain);
        if (variable is not null && symbol is not null)
        {
            symbols[variable] = symbol;
        }
    }

    private static (string? Variable, ExpressionSyntax? Value) ReadStatement(StatementSyntax statement) => statement switch
    {
        LocalDeclarationStatementSyntax { Declaration.Variables: [{ Initializer: { } initializer } declarator] } =>
            (declarator.Identifier.ValueText, initializer.Value),
        ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax { Left: IdentifierNameSyntax target } assignment }
            when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) =>
            (target.Identifier.ValueText, assignment.Right),
        ExpressionStatementSyntax { Expression: var expression } => (null, expression),
        _ => (null, null),
    };

    private object? ReadChain(InvocationChain chain)
    {
        if (chain.Root == "DistributedApplication" && chain.Members is [{ Name: "CreateBuilder" }, ..])
        {
            return BuilderMarker.Instance;
        }

        object? current = symbols.GetValueOrDefault(chain.Root);
        foreach (ChainMember member in chain.Members.Where(member => member.IsCall))
        {
            current = ReadMember(chain.Root, current, member);
        }

        return current;
    }

    private object? ReadMember(string root, object? current, ChainMember member)
    {
        IReadOnlyList<CallArgument> arguments = member.Arguments!;
        string location = $"{SourcePath}:{member.Line}";
        CheckParameterUsage(member, location);
        if (member.Name.StartsWith("Add", StringComparison.Ordinal))
        {
            if (current is not BuilderMarker and not AspireResource)
            {
                Errors.Add($"{location}: receiver of {member.Name} ('{root}') is neither the distributed-application builder nor a resource.");
                return null;
            }

            string? name = arguments.Count > 0 ? arguments[0].StringValue : null;
            if (name is null)
            {
                Errors.Add($"{location}: {member.Name} needs a string-literal name as its first argument.");
                return null;
            }

            return member.Name == "AddParameter"
                ? AddParameter(name, arguments)
                : AddResource(name, member, current as AspireResource, location);
        }

        if (current is not AspireResource resource)
        {
            return current;
        }

        switch (member.Name)
        {
            case "WithReference":
                AddResourceArgument(resource.References, member, location, string.Empty);
                break;
            case "WaitFor":
                AddResourceArgument(resource.WaitsFor, member, location, string.Empty);
                break;
            case "WaitForCompletion":
                AddResourceArgument(resource.WaitsFor, member, location, " (completion)");
                break;
            case "WithHttpHealthCheck":
                CallArgument? path = arguments.FirstOrDefault(argument => argument.Name == "path")
                    ?? arguments.FirstOrDefault(argument => argument.Name is null);
                resource.HealthChecks.Add($"http {(path is null ? "/" : path.StringValue ?? NonLiteral(location, member.Name))}");
                break;
            case "WithEnvironment" when arguments is [{ StringValue: string variable }, { IdentifierValue: string parameterName }]
                && symbols.GetValueOrDefault(parameterName) is AspireParameter parameter:
                parameter.ConsumedAs.Add($"{resource.Name}:{variable}");
                break;
            default:
                break;
        }

        return resource;
    }

    private AspireParameter AddParameter(string name, IReadOnlyList<CallArgument> arguments)
    {
        CallArgument? secret = arguments.FirstOrDefault(argument => argument.Name == "secret")
            ?? (arguments.Count > 1 && arguments[1].Name is null ? arguments[1] : null);
        AspireParameter parameter = new(name, secret?.IsTrue == true);
        Parameters.Add(parameter);
        return parameter;
    }

    private AspireResource AddResource(string name, ChainMember member, AspireResource? parent, string location)
    {
        string project = "-";
        if (member.Name is "AddDotnetProject" or "AddProject")
        {
            string? path = member.Arguments!.Count > 1 ? member.Arguments[1].StringValue : null;
            project = path is null
                ? NonLiteral(location, member.Name)
                : Path.GetFileNameWithoutExtension(path.Replace('\\', '/').Split('/')[^1]);
        }

        AspireResource resource = new(name, member.Name, parent?.Name ?? "-", project);
        Resources.Add(resource);
        return resource;
    }

    private void AddResourceArgument(List<string> target, ChainMember member, string location, string suffix)
    {
        string? variable = member.Arguments is [{ IdentifierValue: string identifier }] ? identifier : null;
        if (variable is not null && symbols.GetValueOrDefault(variable) is AspireResource referenced)
        {
            target.Add(referenced.Name + suffix);
        }
        else if (variable is null || symbols.GetValueOrDefault(variable) is not AspireParameter)
        {
            Errors.Add($"{location}: the argument of {member.Name} must be a variable holding a resource.");
        }
    }

    private void CheckParameterUsage(ChainMember member, string location)
    {
        IEnumerable<CallArgument> parameterArguments = member.Arguments!
            .Where((argument, index) => !(member.Name == "WithEnvironment" && index == 1))
            .Where(argument => argument.Identifiers.Any(identifier => symbols.GetValueOrDefault(identifier) is AspireParameter));
        if (parameterArguments.Any())
        {
            Errors.Add($"{location}: a parameter is passed to {member.Name}; only WithEnvironment(\"<VARIABLE>\", <parameter>) is specified.");
        }
    }

    private string NonLiteral(string location, string call)
    {
        Errors.Add($"{location}: {call} needs a string-literal argument.");
        return "?";
    }

    private static bool IsModelCall(string name) =>
        name.StartsWith("Add", StringComparison.Ordinal) || name.StartsWith("With", StringComparison.Ordinal) || name.StartsWith("WaitFor", StringComparison.Ordinal);

    private sealed class BuilderMarker
    {
        internal static readonly BuilderMarker Instance = new();
    }
}
