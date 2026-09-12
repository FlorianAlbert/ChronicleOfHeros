using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using ChronicleOfHeros.Identity.AspNetCore.Data;
using ChronicleOfHeros.Identity.Contracts;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace ChronicleOfHeros.Identity.AspNetCore.Identity;

// This class gets used by the dependency injection system 
// and may not be directly instantiated.
#pragma warning disable CA1812 // Avoid uninstantiated internal classes

internal sealed class AuthenticationTokenService(
    ChronicleOfHerosDbContext dbContext,
    IOptions<JwtOptions> jwtOptions,
    JwtKeyMaterial keyMaterial,
    TimeProvider timeProvider)
{
    public static readonly TimeSpan NormalAccessTokenLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RestrictedAccessTokenLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan RefreshTokenIdleLifetime = TimeSpan.FromDays(30);
    public static readonly TimeSpan RefreshTokenAbsoluteLifetime = TimeSpan.FromDays(90);

    public RestrictedAccessTokenResponse CreateRestrictedAccessToken(ApplicationUser user)
    {
        DateTimeOffset issuedAt = timeProvider.GetUtcNow();

        return new RestrictedAccessTokenResponse(
            IssueAccessToken(user, issuedAt, RestrictedAccessTokenLifetime, [new Claim("scope", "password-change")]),
            issuedAt.Add(RestrictedAccessTokenLifetime));
    }

    public async Task<TokenPairResponse> CreateNormalTokenPairAsync(
        ApplicationUser user,
        IEnumerable<string> roles,
        CancellationToken cancellationToken)
    {
        DateTimeOffset issuedAt = timeProvider.GetUtcNow();
        DateTimeOffset familyExpiresAt = issuedAt.Add(RefreshTokenAbsoluteLifetime);
        (RefreshSession? refreshSession, string? refreshToken) = CreateRefreshSession(
            user.Id,
            Guid.NewGuid(),
            familyExpiresAt,
            issuedAt);

        _ = dbContext.RefreshSessions.Add(refreshSession);
        _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<Claim> roleClaims = roles.Select(role => new Claim(ClaimTypes.Role, role));
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

        DateTimeOffset refreshedAt = timeProvider.GetUtcNow();
        RefreshedSession? refreshedSession = await dbContext.Database
            .CreateExecutionStrategy()
            .ExecuteAsync(async () =>
            {
                IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await using (transaction.ConfigureAwait(false))
                {
                    RefreshSession? refreshSession = await dbContext.RefreshSessions
                        .Include(session => session.User)
                        .AsNoTracking()
                        .SingleOrDefaultAsync(session => session.TokenHash == Hash(refreshToken), cancellationToken)
                        .ConfigureAwait(false);
                    if (refreshSession is null)
                    {
                        return null;
                    }

                    if (!refreshSession.User.IsActive
                        || refreshSession.RevokedAtUtc is not null
                        || refreshSession.ExpiresAtUtc <= refreshedAt
                        || refreshSession.FamilyExpiresAtUtc <= refreshedAt)
                    {
                        _ = await RevokeRefreshSessionFamilyAsync(refreshSession.FamilyId, refreshedAt, cancellationToken).ConfigureAwait(false);
                        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                        return null;
                    }

                    int tokenWasRevoked = await dbContext.RefreshSessions
                        .Where(session => session.Id == refreshSession.Id && session.RevokedAtUtc == null)
                        .ExecuteUpdateAsync(
                            setters => setters.SetProperty(session => session.RevokedAtUtc, refreshedAt),
                            cancellationToken).ConfigureAwait(false);
                    if (tokenWasRevoked != 1)
                    {
                        _ = await RevokeRefreshSessionFamilyAsync(refreshSession.FamilyId, refreshedAt, cancellationToken).ConfigureAwait(false);
                        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                        return null;
                    }

                    (RefreshSession replacementSession, string replacementToken) = CreateRefreshSession(
                        refreshSession.UserId,
                        refreshSession.FamilyId,
                        refreshSession.FamilyExpiresAtUtc,
                        refreshedAt);
                    _ = dbContext.RefreshSessions.Add(replacementSession);
                    _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                    return new RefreshedSession(refreshSession.User, replacementSession, replacementToken);
                }
            }).ConfigureAwait(false);
        if (refreshedSession is null)
        {
            return null;
        }

        IList<string> roles = await userManager.GetRolesAsync(refreshedSession.User).ConfigureAwait(false);
        IEnumerable<Claim> roleClaims = roles.Select(role => new Claim(ClaimTypes.Role, role));
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

        RefreshSession? refreshSession = await dbContext.RefreshSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(session => session.TokenHash == Hash(refreshToken), cancellationToken)
            .ConfigureAwait(false);
        if (refreshSession is not null)
        {
            _ = await RevokeRefreshSessionFamilyAsync(
                refreshSession.FamilyId,
                timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);
        }
    }

    public Task RevokeAllRefreshSessionsAsync(string userId, CancellationToken cancellationToken)
    {
        DateTimeOffset revokedAt = timeProvider.GetUtcNow();

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
        string token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));

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
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, issuedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            .. additionalClaims,
        ];

        JwtSecurityToken token = new(
            issuer: jwtOptions.Value.Issuer,
            audience: jwtOptions.Value.Audience,
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