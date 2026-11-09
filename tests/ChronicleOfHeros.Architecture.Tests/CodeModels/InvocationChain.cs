using System.Globalization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ChronicleOfHeros.Architecture.Tests.CodeModels;

/// <summary>
/// One argument of an invocation, optionally named (<c>name: value</c>).
/// </summary>
internal sealed record CallArgument(string? Name, ExpressionSyntax Expression)
{
    /// <summary>
    /// Gets the literal value when the argument is a string literal; otherwise <see langword="null"/>.
    /// </summary>
    internal string? StringValue => Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
        ? literal.Token.ValueText
        : null;

    /// <summary>
    /// Gets the name when the argument is a single identifier; otherwise <see langword="null"/>.
    /// </summary>
    internal string? IdentifierValue => Expression is IdentifierNameSyntax identifier ? identifier.Identifier.ValueText : null;

    /// <summary>
    /// Gets a value indicating whether the argument is the literal <see langword="true"/>.
    /// </summary>
    internal bool IsTrue => Expression.IsKind(SyntaxKind.TrueLiteralExpression);

    /// <summary>
    /// Gets every identifier the argument mentions.
    /// </summary>
    internal IEnumerable<string> Identifiers => Expression
        .DescendantNodesAndSelf()
        .OfType<IdentifierNameSyntax>()
        .Select(identifier => identifier.Identifier.ValueText);
}

/// <summary>
/// One member access in a chain; <see cref="Arguments"/> is <see langword="null"/> for a property or field access.
/// </summary>
internal sealed record ChainMember(string Name, IReadOnlyList<CallArgument>? Arguments, int Line, SyntaxNode Node)
{
    /// <summary>
    /// Gets a value indicating whether the member is invoked.
    /// </summary>
    internal bool IsCall => Arguments is not null;
}

/// <summary>
/// An expression of the form <c>root.Member(args).Member…</c>; a root that is itself invoked becomes the first member.
/// </summary>
internal sealed record InvocationChain(string Root, IReadOnlyList<ChainMember> Members)
{
    /// <summary>
    /// Reads an expression that is exactly one invocation chain, or returns <see langword="null"/>.
    /// </summary>
    internal static InvocationChain? Read(ExpressionSyntax expression)
    {
        List<ChainMember> members = [];
        ExpressionSyntax current = expression;
        while (true)
        {
            switch (current)
            {
                case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } invocation
                    when access.IsKind(SyntaxKind.SimpleMemberAccessExpression):
                    members.Add(new ChainMember(access.Name.Identifier.ValueText, ToArguments(invocation.ArgumentList), access.Name.Line, access.Name));
                    current = access.Expression;
                    break;
                case InvocationExpressionSyntax { Expression: SimpleNameSyntax name } invocation:
                    members.Add(new ChainMember(name.Identifier.ValueText, ToArguments(invocation.ArgumentList), name.Line, name));
                    members.Reverse();
                    return new InvocationChain(name.Identifier.ValueText, members);
                case MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression):
                    members.Add(new ChainMember(access.Name.Identifier.ValueText, null, access.Name.Line, access.Name));
                    current = access.Expression;
                    break;
                case IdentifierNameSyntax root:
                    members.Reverse();
                    return new InvocationChain(root.Identifier.ValueText, members);
                default:
                    return null;
            }
        }
    }

    private static CallArgument[] ToArguments(ArgumentListSyntax arguments) =>
        [.. arguments.Arguments.Select(argument => new CallArgument(argument.NameColon?.Name.Identifier.ValueText, argument.Expression))];
}

/// <summary>
/// Parses C# source for the code models and reports syntax errors with file and line.
/// </summary>
internal static class CSharpSource
{
    private static readonly CSharpParseOptions Options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

    /// <summary>
    /// Parses <paramref name="source"/> and adds each error diagnostic to <paramref name="errors"/>.
    /// </summary>
    internal static CompilationUnitSyntax Parse(string path, string source, List<string> errors)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, Options, path);
        errors.AddRange(tree
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => $"{path}:{diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1}: C# syntax error {diagnostic.Id}: {diagnostic.GetMessage(CultureInfo.InvariantCulture)}"));
        return tree.GetCompilationUnitRoot();
    }
}

/// <summary>
/// Source positions of syntax nodes.
/// </summary>
internal static class SyntaxNodeLineExtensions
{
    extension(SyntaxNode node)
    {
        /// <summary>
        /// Gets the one-based line on which the node starts, excluding leading trivia.
        /// </summary>
        internal int Line => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    }
}
