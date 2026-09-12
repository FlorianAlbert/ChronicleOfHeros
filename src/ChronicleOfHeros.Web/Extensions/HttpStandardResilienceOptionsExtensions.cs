using Microsoft.Extensions.Http.Resilience;

namespace ChronicleOfHeros.Web.Extensions;

internal static class HttpStandardResilienceOptionsExtensions
{
    extension(HttpStandardResilienceOptions options)
    {
        internal void DisableRetries() => options.Retry.MaxRetryAttempts = 0;
    }
}