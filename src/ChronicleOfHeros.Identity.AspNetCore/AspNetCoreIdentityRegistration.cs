using ChronicleOfHeros.Identity.AspNetCore.DependencyInjection;

using Microsoft.Extensions.Hosting;

namespace ChronicleOfHeros.Identity.AspNetCore;

/// <summary>
/// Options for configuring the registration of the Chronicle of Heroes Identity services in an ASP.NET Core application.
/// </summary>
public sealed class AspNetCoreIdentityRegistrationOptions
{
    internal bool MigrationsEnabled { get; private set; }

    /// <summary>
    /// Enables database migrations for the Chronicle of Heroes Identity services. When enabled, the application will automatically apply any pending migrations to the database on startup.
    /// </summary>
    public void EnableMigrations() => MigrationsEnabled = true;
}

/// <summary>
/// Provides extension methods for registering the Chronicle of Heroes Identity services in an ASP.NET Core application.
/// </summary>
public static class AspNetCoreIdentityRegistration
{
    extension(IHostApplicationBuilder builder)
    {
        /// <summary>
        /// Registers the Chronicle of Heroes Identity services in the ASP.NET Core application. This includes configuring the database context, authentication, authorization, and other related services.
        /// </summary>
        /// <param name="configure">An action to configure the registration options.</param>
        /// <returns>The host application builder.</returns>
        public IHostApplicationBuilder AddAspNetCoreIdentity(
            Action<AspNetCoreIdentityRegistrationOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            return IdentityCapabilityRegistration.Register(builder, configure);
        }
    }
}