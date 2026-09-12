using System.Security.Cryptography;

using Microsoft.IdentityModel.Tokens;

namespace ChronicleOfHeros.Identity.AspNetCore.Identity;

internal sealed class JwtKeyMaterial : IDisposable
{
    private readonly RSA signingRsa;
    private readonly RSA validationRsa;

    private JwtKeyMaterial(RSA signingRsa, RSA validationRsa)
    {
        this.signingRsa = signingRsa;
        this.validationRsa = validationRsa;
        SigningCredentials = new SigningCredentials(
            new RsaSecurityKey(signingRsa)
            {
                CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
            },
            SecurityAlgorithms.RsaSha256);
        ValidationKey = new RsaSecurityKey(validationRsa)
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
        };
    }

    public SigningCredentials SigningCredentials { get; }

    public SecurityKey ValidationKey { get; }

    public static JwtKeyMaterial Create(JwtOptions options)
    {
        RSA signingRsa = RSA.Create();
        signingRsa.ImportPkcs8PrivateKey(Convert.FromBase64String(options.SigningPrivateKey!), out _);

        RSA validationRsa = RSA.Create();
        validationRsa.ImportSubjectPublicKeyInfo(signingRsa.ExportSubjectPublicKeyInfo(), out _);

        return new JwtKeyMaterial(signingRsa, validationRsa);
    }

    public void Dispose()
    {
        signingRsa.Dispose();
        validationRsa.Dispose();
    }
}