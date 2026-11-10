using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace ChronicleOfHeros.ServiceDefaults;

/// <summary>
/// Named configuration members that the service defaults apply to framework options and builders.
/// </summary>
internal static class ServiceDefaultsOptionsExtensions
{
    internal const string HealthEndpointPath = "/health";
    internal const string AlivenessEndpointPath = "/alive";

    extension(IHttpClientBuilder http)
    {
        /// <summary>
        /// Adds the standard resilience handler and service discovery to the HTTP client.
        /// </summary>
        internal void AddResilienceAndServiceDiscovery()
        {
            _ = http.AddStandardResilienceHandler();
            _ = http.AddServiceDiscovery();
        }
    }

    extension(OpenTelemetryLoggerOptions logging)
    {
        /// <summary>
        /// Includes formatted messages and scopes in exported log records.
        /// </summary>
        internal void IncludeFormattedMessagesAndScopes()
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        }
    }

    extension(MeterProviderBuilder metrics)
    {
        /// <summary>
        /// Adds ASP.NET Core, HTTP client, and runtime metrics instrumentation.
        /// </summary>
        internal void AddDefaultMetricsInstrumentation() =>
            _ = metrics.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();
    }

    extension(TracerProviderBuilder tracing)
    {
        /// <summary>
        /// Adds tracing for the application's own activity source, ASP.NET Core requests except health endpoints, and HTTP clients.
        /// </summary>
        /// <param name="applicationName">The name of the application's activity source.</param>
        internal void AddDefaultTracingInstrumentation(string applicationName) =>
            _ = tracing.AddSource(applicationName)
                .AddAspNetCoreInstrumentation(options => options.ExcludeHealthEndpoints())
                .AddHttpClientInstrumentation();
    }

    extension(AspNetCoreTraceInstrumentationOptions options)
    {
        /// <summary>
        /// Excludes requests to the health and aliveness endpoints from tracing.
        /// </summary>
        internal void ExcludeHealthEndpoints() =>
            options.Filter = context =>
                !context.Request.Path.StartsWithSegments(HealthEndpointPath, StringComparison.OrdinalIgnoreCase) &&
                !context.Request.Path.StartsWithSegments(AlivenessEndpointPath, StringComparison.OrdinalIgnoreCase);
    }
}
