using Microsoft.Extensions.Http.Resilience;

namespace ChronicleOfHeros.Web.Services;

internal static class HttpStandardResilienceOptionsExtensions
{
    extension(HttpStandardResilienceOptions options)
    {
        internal void DisableRetries() => options.Retry.MaxRetryAttempts = 0;
    }
}