using Aspire.Npgsql.EntityFrameworkCore.PostgreSQL;
using ChronicleOfHeros.Identity.AspNetCore.Data;
using ChronicleOfHeros.Identity.AspNetCore.Identity;
using ChronicleOfHeros.Identity.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ChronicleOfHeros.Identity.AspNetCore;

public sealed class AspNetCoreIdentityRegistrationOptions
{
    internal bool MigrationsEnabled { get; private set; }

    public void EnableMigrations() => MigrationsEnabled = true;
}

public static class AspNetCoreIdentityRegistration
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddAspNetCoreIdentity(
            Action<AspNetCoreIdentityRegistrationOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            var registrationOptions = new AspNetCoreIdentityRegistrationOptions();
            configure?.Invoke(registrationOptions);

            builder.AddNpgsqlDbContext<ChronicleOfHerosDbContext>(
                IdentityPersistence.ConnectionName,
                options => options.ConfigureIdentityPersistence());
            if (registrationOptions.MigrationsEnabled)
            {
                builder.Services.AddHostedService<IdentityMigrationHostedService>();
                return builder;
            }

            builder.Services.AddOptions<JwtOptions>()
                .Configure(options => options.ConfigureFrom(builder.Configuration))
                .Validate(options => options.HasRequiredValues(), "JWT configuration is required.")
                .ValidateOnStart();
            builder.Services.Configure<BootstrapOperatorOptions>(options =>
                options.ConfigureFrom(builder.Configuration));
            builder.Services.AddSingleton<JwtKeyMaterial>(services =>
                JwtKeyMaterial.Create(services.GetRequiredService<IOptions<JwtOptions>>().Value));
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddScoped<AuthenticationTokenService>();
            builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
            builder.Services.AddScoped<IPlayerAdministrationService, PlayerAdministrationService>();
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();
            builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtOptions>, JwtKeyMaterial>((options, jwtOptions, jwtKeyMaterial) =>
                    options.ConfigureIdentityJwt(jwtOptions.Value, jwtKeyMaterial));
            builder.Services.AddAuthorization(options => options.ConfigureIdentityPolicies());
            builder.Services.AddIdentityCore<ApplicationUser>(options => options.ConfigureIdentityPasswords())
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ChronicleOfHerosDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.AddHostedService<BootstrapOperatorHostedService>();

            return builder;
        }
    }
}