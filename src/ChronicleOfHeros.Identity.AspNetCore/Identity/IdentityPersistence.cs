using Aspire.Npgsql.EntityFrameworkCore.PostgreSQL;

namespace ChronicleOfHeros.Identity.AspNetCore.Identity;

internal static class IdentityPersistence
{
    internal const string ConnectionName = "chronicleofheros";
}

internal static class IdentityPersistenceOptionsExtensions
{
    extension(NpgsqlEntityFrameworkCorePostgreSQLSettings options)
    {
        internal void ConfigureIdentityPersistence() => options.DisableRetry = false;
    }
}