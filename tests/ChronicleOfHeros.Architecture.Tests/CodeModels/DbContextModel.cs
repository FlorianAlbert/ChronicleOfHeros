using System.Reflection;

using Microsoft.EntityFrameworkCore;

namespace ChronicleOfHeros.Architecture.Tests.CodeModels;

/// <summary>
/// Reads production <see cref="DbContext"/> models per docs/architecture/conventions.md § <c>dbcontext-entities:&lt;DbContext&gt;</c>.
/// </summary>
internal static class DbContextModel
{
    private const string PlaceholderConnectionString = "Host=localhost;Database=architecture-tests";

    private static readonly Dictionary<Type, string> Aliases = new()
    {
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(char)] = "char",
        [typeof(decimal)] = "decimal",
        [typeof(double)] = "double",
        [typeof(float)] = "float",
        [typeof(int)] = "int",
        [typeof(long)] = "long",
        [typeof(object)] = "object",
        [typeof(short)] = "short",
        [typeof(string)] = "string",
    };

    /// <summary>
    /// Gets every non-abstract <see cref="DbContext"/> subclass in a production assembly of the solution.
    /// </summary>
    internal static IReadOnlyList<Type> ProductionContexts() =>
        [.. SolutionProjects.Production
            .SelectMany(project => LoadableTypes(project.LoadAssembly()))
            .Where(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(DbContext)))
            .OrderBy(type => type.Name, StringComparer.Ordinal)];

    /// <summary>
    /// Returns the (entity, table) pairs of the context's EF Core model, built with Npgsql and a placeholder connection string.
    /// </summary>
    internal static IReadOnlyList<(string Entity, string Table)> Entities(Type contextType)
    {
        DbContextOptionsBuilder optionsBuilder = (DbContextOptionsBuilder)Activator.CreateInstance(
            typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType))!;
        DbContextOptions options = optionsBuilder.UseNpgsql(PlaceholderConnectionString).Options;
        using DbContext context = (DbContext)Activator.CreateInstance(
            contextType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [options],
            culture: null)!;
        return [.. context.Model.GetEntityTypes()
            .Select(entityType => (Format(entityType.ClrType), entityType.GetTableName() ?? "-"))];
    }

    /// <summary>
    /// Formats a type name without namespace, writing generics as <c>Name&lt;Arg&gt;</c> with C# keyword aliases.
    /// </summary>
    internal static string Format(Type type) =>
        Aliases.TryGetValue(type, out string? alias)
            ? alias
            : type.IsGenericType
                ? $"{type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)]}<{string.Join(", ", type.GetGenericArguments().Select(Format))}>"
                : type.Name;

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }
}
