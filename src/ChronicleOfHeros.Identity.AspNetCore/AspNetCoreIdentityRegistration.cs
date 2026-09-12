using ChronicleOfHeros.Identity.AspNetCore.Data;
using ChronicleOfHeros.Identity.AspNetCore.Identity;
using ChronicleOfHeros.Identity.Contracts;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
            AspNetCoreIdentityRegistrationOptions registrationOptions = new();
            configure?.Invoke(registrationOptions);

            builder.AddNpgsqlDbContext<ChronicleOfHerosDbContext>(
                IdentityPersistence.ConnectionName,
                options => options.ConfigureIdentityPersistence());
            if (registrationOptions.MigrationsEnabled)
            {
                _ = builder.Services.AddHostedService<IdentityMigrationHostedService>();
                return builder;
            }

            _ = builder.Services.AddOptions<JwtOptions>()
                .Configure(options => options.ConfigureFrom(builder.Configuration))
                .Validate(options => options.HasRequiredValues(), "JWT configuration is required.")
                .ValidateOnStart();
            _ = builder.Services.Configure<BootstrapOperatorOptions>(options =>
                options.ConfigureFrom(builder.Configuration));
            _ = builder.Services.AddSingleton(services =>
                JwtKeyMaterial.Create(services.GetRequiredService<IOptions<JwtOptions>>().Value));
            _ = builder.Services.AddSingleton(TimeProvider.System);
            _ = builder.Services.AddScoped<AuthenticationTokenService>();
            _ = builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
            _ = builder.Services.AddScoped<IPlayerAdministrationService, PlayerAdministrationService>();
            _ = builder.Services.AddScoped<IPlayerIdentityService, PlayerIdentityService>();
            _ = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();
            _ = builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtOptions>, JwtKeyMaterial>((options, jwtOptions, jwtKeyMaterial) =>
                    options.ConfigureIdentityJwt(jwtOptions.Value, jwtKeyMaterial));
            _ = builder.Services.AddAuthorization(options => options.ConfigureIdentityPolicies());
            _ = builder.Services.AddIdentityCore<ApplicationUser>(options => options.ConfigureIdentityPasswords())
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ChronicleOfHerosDbContext>()
                .AddDefaultTokenProviders();

            _ = builder.Services.AddHostedService<BootstrapOperatorHostedService>();

            return builder;
        }
    }
}