using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ChronicleOfHeros.Web.Authentication;

// This class gets used by the dependency injection system.
#pragma warning disable CA1812 // Avoid uninstantiated internal classes

internal sealed class BrowserSessionPrincipalFactory : IDisposable
{
    private readonly ECDsa _validationKey;
    private readonly TokenValidationParameters _validationParameters;

    public BrowserSessionPrincipalFactory(IOptions<BrowserSessionOptions> options)
    {
        BrowserSessionOptions browserSessionOptions = options.Value;
        _validationKey = ECDsa.Create();
        _validationKey.ImportSubjectPublicKeyInfo(
            Convert.FromBase64String(browserSessionOptions.SigningPublicKey!),
            out _);
        _validationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new ECDsaSecurityKey(_validationKey)
            {
                CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
            },
            ValidIssuer = browserSessionOptions.Issuer,
            ValidAudience = browserSessionOptions.Audience,
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role,
        };
    }

    internal ClaimsPrincipal? Create(string accessToken)
    {
        try
        {
            return new JwtSecurityTokenHandler().ValidateToken(accessToken, _validationParameters, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    public void Dispose() => _validationKey.Dispose();
}

#pragma warning restore CA1812 // Avoid uninstantiated internal classes
