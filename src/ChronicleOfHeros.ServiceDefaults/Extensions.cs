using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.Hosting;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for configuring service defaults in a host application builder.
/// </summary>
public static class ServiceDefaultsExtensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    extension<TBuilder>(TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        /// <summary>
        /// Adds service defaults to the host application builder, including OpenTelemetry configuration, default health checks, service discovery, and HTTP client defaults.
        /// </summary>
        /// <returns>The modified host application builder.</returns>
        public TBuilder AddServiceDefaults()
        {
            _ = builder.ConfigureOpenTelemetry();
            _ = builder.AddDefaultHealthChecks();
            _ = builder.Services.AddServiceDiscovery()
                                .ConfigureHttpClientDefaults(http =>
                                {
                                    _ = http.AddStandardResilienceHandler();
                                    _ = http.AddServiceDiscovery();
                                });

            return builder;
        }

        /// <summary>
        /// Configures OpenTelemetry for the host application builder, including metrics and tracing instrumentation.
        /// </summary>
        /// <returns>The modified host application builder.</returns>
        public TBuilder ConfigureOpenTelemetry()
        {
            _ = builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });

            _ = builder.Services.AddOpenTelemetry()
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation())
                .WithTracing(tracing => tracing
                    .AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(options => options.Filter = context =>
                        !context.Request.Path.StartsWithSegments(HealthEndpointPath, StringComparison.OrdinalIgnoreCase) &&
                        !context.Request.Path.StartsWithSegments(AlivenessEndpointPath, StringComparison.OrdinalIgnoreCase))
                    .AddHttpClientInstrumentation());

            _ = AddOpenTelemetryExporters(builder);

            return builder;
        }

        /// <summary>
        /// Adds default health checks to the host application builder, including a self-check and an aliveness check.
        /// The self-check is always healthy, while the aliveness check is tagged with "live" and can be used to determine if the application is alive.
        /// </summary>
        /// <returns>The modified host application builder.</returns>
        public TBuilder AddDefaultHealthChecks()
        {
            _ = builder.Services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

            return builder;
        }
    }

    extension(WebApplication app)
    {
        /// <summary>
        /// Maps the default health check endpoints to the web application. In development environment, it maps the /health endpoint for overall health and the /alive endpoint for aliveness checks.
        /// </summary>
        /// <returns>The modified web application.</returns>
        public WebApplication MapDefaultEndpoints()
        {
            if (app.Environment.IsDevelopment())
            {
                _ = app.MapHealthChecks(HealthEndpointPath);
                _ = app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
                {
                    Predicate = registration => registration.Tags.Contains("live")
                });
            }

            return app;
        }
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            _ = builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }
}