using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace ChronicleOfHeros.Identity.AspNetCore.Identity;

internal static class IdentityRegistrationOptionsExtensions
{
    extension(JwtBearerOptions options)
    {
        internal void ConfigureIdentityJwt(JwtOptions jwtOptions, JwtKeyMaterial jwtKeyMaterial)
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = jwtKeyMaterial.ValidationKey,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = ClaimTypes.Role,
            };
        }
    }

    extension(AuthorizationOptions options)
    {
        internal void ConfigureIdentityPolicies()
        {
            options.AddPolicy("Player", policy => policy.RequireRole(ApplicationRoles.Player));
            options.AddPolicy(
                "PasswordChange",
                policy => policy.RequireAssertion(context =>
                    context.User.IsInRole(ApplicationRoles.Player)
                    || context.User.HasClaim("scope", "password-change")));
            options.AddPolicy("Operator", policy => policy.RequireRole(ApplicationRoles.Operator));
        }
    }

    extension(IdentityOptions options)
    {
        internal void ConfigureIdentityPasswords()
        {
            options.Password.RequiredLength = 8;
            options.Password.RequiredUniqueChars = 1;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
        }
    }
}