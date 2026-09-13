using System.Security.Cryptography;
using System.Text.Json;

using ChronicleOfHeros.Identity.Contracts;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;

namespace ChronicleOfHeros.Web.Authentication;

// This class gets used by the dependency injection system.
#pragma warning disable CA1812 // Avoid uninstantiated internal classes

internal sealed partial class BrowserSessionStore(
    IDistributedCache cache,
    IDataProtectionProvider dataProtectionProvider,
    ILogger<BrowserSessionStore> logger)
{
    private const string CacheKeyPrefix = "browser-session:";

    private readonly IDataProtector _dataProtector = dataProtectionProvider.CreateProtector("BrowserSessionStore.v1");

    internal async Task<string> CreateAsync(TokenPairResponse tokenPair, CancellationToken cancellationToken)
    {
        string sessionId = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        BrowserSessionRecord record = new(
            tokenPair.AccessToken,
            tokenPair.AccessTokenExpiresAt,
            tokenPair.RefreshToken,
            tokenPair.RefreshTokenExpiresAt);
        byte[] protectedRecord = _dataProtector.Protect(JsonSerializer.SerializeToUtf8Bytes(record));
        DistributedCacheEntryOptions options = new()
        {
            AbsoluteExpiration = tokenPair.RefreshTokenExpiresAt,
        };

        await cache.SetAsync(GetCacheKey(sessionId), protectedRecord, options, cancellationToken).ConfigureAwait(false);
        return sessionId;
    }

    internal async Task<BrowserSessionRecord?> GetAsync(string sessionId, CancellationToken cancellationToken)
    {
        byte[]? protectedRecord = await cache.GetAsync(GetCacheKey(sessionId), cancellationToken).ConfigureAwait(false);
        if (protectedRecord is null)
        {
            return null;
        }

        try
        {
            BrowserSessionRecord? record = JsonSerializer.Deserialize<BrowserSessionRecord>(
                _dataProtector.Unprotect(protectedRecord));
            if (record is not null)
            {
                return record;
            }
        }
        catch (CryptographicException exception)
        {
            LogCannotUnprotectSession(logger, exception, sessionId);
        }
        catch (JsonException exception)
        {
            LogInvalidSessionRecord(logger, exception, sessionId);
        }

        await DeleteAsync(sessionId, cancellationToken).ConfigureAwait(false);
        return null;
    }

    internal Task DeleteAsync(string sessionId, CancellationToken cancellationToken) =>
        cache.RemoveAsync(GetCacheKey(sessionId), cancellationToken);

    private static string GetCacheKey(string sessionId) => $"{CacheKeyPrefix}{sessionId}";

    [LoggerMessage(LogLevel.Warning, "Browser session {BrowserSessionId} could not be unprotected.")]
    private static partial void LogCannotUnprotectSession(
        ILogger logger,
        CryptographicException exception,
        string browserSessionId);

    [LoggerMessage(LogLevel.Warning, "Browser session {BrowserSessionId} had an invalid record.")]
    private static partial void LogInvalidSessionRecord(
        ILogger logger,
        JsonException exception,
        string browserSessionId);
}

internal sealed record BrowserSessionRecord(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

#pragma warning restore CA1812 // Avoid uninstantiated internal classes
