using System.Security.Cryptography;

using Microsoft.IdentityModel.Tokens;

namespace ChronicleOfHeros.Identity.AspNetCore.Infrastructure.Identity;

internal sealed class JwtKeyMaterial : IDisposable
{
    private readonly ECDsa signingKey;
    private readonly ECDsa validationKey;

    private JwtKeyMaterial(ECDsa signingKey, ECDsa validationKey)
    {
        this.signingKey = signingKey;
        this.validationKey = validationKey;
        SigningCredentials = new SigningCredentials(
            new ECDsaSecurityKey(signingKey)
            {
                CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
            },
            SecurityAlgorithms.EcdsaSha256);
        ValidationKey = new ECDsaSecurityKey(validationKey)
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
        };
    }

    public SigningCredentials SigningCredentials { get; }

    public SecurityKey ValidationKey { get; }

    public static JwtKeyMaterial Create(JwtOptions options)
    {
        ECDsa signingKey = ECDsa.Create();
        signingKey.ImportPkcs8PrivateKey(Convert.FromBase64String(options.SigningPrivateKey!), out _);

        ECDsa validationKey = ECDsa.Create();
        validationKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(options.SigningPublicKey!), out _);

        return new JwtKeyMaterial(signingKey, validationKey);
    }

    public void Dispose()
    {
        signingKey.Dispose();
        validationKey.Dispose();
    }
}