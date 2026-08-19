using ChronicleOfHeros.Api.Data;
using Microsoft.AspNetCore.Identity;
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
    public static readonly TimeSpan RefreshTokenIdleLifetime = TimeSpan.FromDays(30);
    public static readonly TimeSpan RefreshTokenAbsoluteLifetime = TimeSpan.FromDays(90);

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
        var familyExpiresAt = issuedAt.Add(RefreshTokenAbsoluteLifetime);
        var (refreshSession, refreshToken) = CreateRefreshSession(
            user.Id,
            Guid.NewGuid(),
            familyExpiresAt,
            issuedAt);

        dbContext.RefreshSessions.Add(refreshSession);
        await dbContext.SaveChangesAsync(cancellationToken);

        var roleClaims = roles.Select(role => new Claim(ClaimTypes.Role, role));
        return new TokenPairResponse(
            IssueAccessToken(user, issuedAt, NormalAccessTokenLifetime, roleClaims),
            issuedAt.Add(NormalAccessTokenLifetime),
            refreshToken,
            refreshSession.ExpiresAtUtc);
    }

    public async Task<TokenPairResponse?> RefreshNormalTokenPairAsync(
        string? refreshToken,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var refreshedAt = timeProvider.GetUtcNow();
        var refreshedSession = await dbContext.Database
            .CreateExecutionStrategy()
            .ExecuteAsync(async () =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                var refreshSession = await dbContext.RefreshSessions
                    .Include(session => session.User)
                    .AsNoTracking()
                    .SingleOrDefaultAsync(session => session.TokenHash == Hash(refreshToken), cancellationToken);
                if (refreshSession is null)
                {
                    return null;
                }

                if (!refreshSession.User.IsActive
                    || refreshSession.RevokedAtUtc is not null
                    || refreshSession.ExpiresAtUtc <= refreshedAt
                    || refreshSession.FamilyExpiresAtUtc <= refreshedAt)
                {
                    await RevokeRefreshSessionFamilyAsync(refreshSession.FamilyId, refreshedAt, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return null;
                }

                var tokenWasRevoked = await dbContext.RefreshSessions
                    .Where(session => session.Id == refreshSession.Id && session.RevokedAtUtc == null)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(session => session.RevokedAtUtc, refreshedAt),
                        cancellationToken);
                if (tokenWasRevoked != 1)
                {
                    await RevokeRefreshSessionFamilyAsync(refreshSession.FamilyId, refreshedAt, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return null;
                }

                var (replacementSession, replacementToken) = CreateRefreshSession(
                    refreshSession.UserId,
                    refreshSession.FamilyId,
                    refreshSession.FamilyExpiresAtUtc,
                    refreshedAt);
                dbContext.RefreshSessions.Add(replacementSession);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new RefreshedSession(refreshSession.User, replacementSession, replacementToken);
            });
        if (refreshedSession is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(refreshedSession.User);
        var roleClaims = roles.Select(role => new Claim(ClaimTypes.Role, role));
        return new TokenPairResponse(
            IssueAccessToken(
                refreshedSession.User,
                refreshedAt,
                NormalAccessTokenLifetime,
                roleClaims),
            refreshedAt.Add(NormalAccessTokenLifetime),
            refreshedSession.ReplacementToken,
            refreshedSession.ReplacementSession.ExpiresAtUtc);
    }

    public async Task RevokeRefreshSessionFamilyForTokenAsync(
        string? refreshToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var refreshSession = await dbContext.RefreshSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(session => session.TokenHash == Hash(refreshToken), cancellationToken);
        if (refreshSession is not null)
        {
            await RevokeRefreshSessionFamilyAsync(
                refreshSession.FamilyId,
                timeProvider.GetUtcNow(),
                cancellationToken);
        }
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

    private Task<int> RevokeRefreshSessionFamilyAsync(
        Guid familyId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken) =>
        dbContext.RefreshSessions
            .Where(session => session.FamilyId == familyId && session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(session => session.RevokedAtUtc, revokedAt),
                cancellationToken);

    private static (RefreshSession Session, string Token) CreateRefreshSession(
        string userId,
        Guid familyId,
        DateTimeOffset familyExpiresAt,
        DateTimeOffset createdAt)
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));

        return (new RefreshSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(token),
            FamilyId = familyId,
            CreatedAtUtc = createdAt,
            ExpiresAtUtc = Min(createdAt.Add(RefreshTokenIdleLifetime), familyExpiresAt),
            FamilyExpiresAtUtc = familyExpiresAt,
        }, token);
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

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) =>
        first <= second ? first : second;

    private sealed record RefreshedSession(
        ApplicationUser User,
        RefreshSession ReplacementSession,
        string ReplacementToken);
}

public sealed record SignInRequest(string? Username, string? Password);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record RefreshTokenRequest(string? RefreshToken);

public sealed record EnrollPlayerRequest(string? Username);

public sealed record RestrictedAccessTokenResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt);

public sealed record TokenPairResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record TemporaryCredentialResponse(string TemporaryCredential);

public sealed record PlayerIdentityResponse(Guid AccountId);