using ChronicleOfHeros.Api.Data;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ChronicleOfHeros.Api.Identity;

public sealed class JwtOptions
{
    public const string ConfigurationSectionName = "Jwt";

    public required string SigningPrivateKey { get; init; }

    public required string Issuer { get; init; }

    public required string Audience { get; init; }
}

public sealed class JwtKeyMaterial : IDisposable
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
        var signingRsa = RSA.Create();
        signingRsa.ImportPkcs8PrivateKey(Convert.FromBase64String(options.SigningPrivateKey), out _);

        var validationRsa = RSA.Create();
        validationRsa.ImportSubjectPublicKeyInfo(signingRsa.ExportSubjectPublicKeyInfo(), out _);

        return new JwtKeyMaterial(signingRsa, validationRsa);
    }

    public void Dispose()
    {
        signingRsa.Dispose();
        validationRsa.Dispose();
    }
}

public sealed class AuthenticationTokenService(
    ChronicleOfHerosDbContext dbContext,
    JwtOptions jwtOptions,
    JwtKeyMaterial keyMaterial,
    TimeProvider timeProvider)
{
    public static readonly TimeSpan NormalAccessTokenLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RestrictedAccessTokenLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public RestrictedAccessTokenResponse CreateRestrictedAccessToken(ApplicationUser user)
    {
        var issuedAt = timeProvider.GetUtcNow();

        return new RestrictedAccessTokenResponse(
            IssueAccessToken(user, issuedAt, RestrictedAccessTokenLifetime, [new Claim("scope", "password-change")]),
            issuedAt.Add(RestrictedAccessTokenLifetime));
    }

    public async Task<TokenPairResponse> CreateNormalTokenPairAsync(
        ApplicationUser user,
        IEnumerable<string> roles,
        CancellationToken cancellationToken)
    {
        var issuedAt = timeProvider.GetUtcNow();
        var refreshToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        var refreshTokenExpiresAt = issuedAt.Add(RefreshTokenLifetime);

        dbContext.RefreshSessions.Add(new RefreshSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Hash(refreshToken),
            FamilyId = Guid.NewGuid(),
            CreatedAtUtc = issuedAt,
            ExpiresAtUtc = refreshTokenExpiresAt,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var roleClaims = roles.Select(role => new Claim(ClaimTypes.Role, role));
        return new TokenPairResponse(
            IssueAccessToken(user, issuedAt, NormalAccessTokenLifetime, roleClaims),
            issuedAt.Add(NormalAccessTokenLifetime),
            refreshToken,
            refreshTokenExpiresAt);
    }

    public Task RevokeAllRefreshSessionsAsync(string userId, CancellationToken cancellationToken)
    {
        var revokedAt = timeProvider.GetUtcNow();

        return dbContext.RefreshSessions
            .Where(session => session.UserId == userId && session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(session => session.RevokedAtUtc, revokedAt),
                cancellationToken);
    }

    private string IssueAccessToken(
        ApplicationUser user,
        DateTimeOffset issuedAt,
        TimeSpan lifetime,
        IEnumerable<Claim> additionalClaims)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, issuedAt.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };
        claims.AddRange(additionalClaims);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: issuedAt.Add(lifetime).UtcDateTime,
            signingCredentials: keyMaterial.SigningCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed record SignInRequest(string? Username, string? Password);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record RestrictedAccessTokenResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt);

public sealed record TokenPairResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);