# Cross-cutting: observability and resilience

Scope: shared service defaults: OpenTelemetry, health checks, service discovery, and HTTP resilience, and which hosts use them.
Load when: changing `ChronicleOfHeros.ServiceDefaults`, telemetry, health endpoints, service discovery, or HTTP client resilience.
Parent: [ARCHITECTURE.md](../../../ARCHITECTURE.md) · Neighbours: [localization.md](localization.md), [security.md](security.md), [../system/deployment.md](../system/deployment.md)

`ChronicleOfHeros.ServiceDefaults` is an Aspire shared project (`IsAspireSharedProject`) with one public extension container, `ServiceDefaultsExtensions` in `ServiceDefaultsExtensions.cs`, declared in namespace `Microsoft.Extensions.Hosting`. Every configuration callback it passes calls one named member of the internal container `ServiceDefaultsOptionsExtensions` (namespace `ChronicleOfHeros.ServiceDefaults`), which also holds the `/health` and `/alive` paths.

| Member | Effect |
|---|---|
| `AddServiceDefaults()` | `ConfigureOpenTelemetry()`, `AddDefaultHealthChecks()`, `AddServiceDiscovery()`, and HTTP client defaults through `AddResilienceAndServiceDiscovery()`: standard resilience handler plus service discovery on every `IHttpClientFactory` client |
| `ConfigureOpenTelemetry()` | Logs with formatted messages and scopes (`IncludeFormattedMessagesAndScopes()`); metrics for ASP.NET Core, `HttpClient`, and runtime (`AddDefaultMetricsInstrumentation()`); traces for the application's own `ActivitySource`, ASP.NET Core (excluding `/health` and `/alive` through `ExcludeHealthEndpoints()`), and `HttpClient` (`AddDefaultTracingInstrumentation(applicationName)`). Adds the OTLP exporter only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set |
| `AddDefaultHealthChecks()` | Health check `self`, always healthy, tagged `live` |
| `MapDefaultEndpoints()` | Development only: `/health` runs all checks; `/alive` runs checks tagged `live`. Elsewhere it maps nothing |

| Host | `AddServiceDefaults` | `MapDefaultEndpoints` | Notes |
|---|---|---|---|
| API | yes | yes | The Aspire Npgsql integration adds a database health check to `/health`. The AppHost polls `/health` ([../system/deployment.md](../system/deployment.md)). |
| Web | yes | yes | Creates no `HttpClient` and calls no other resource ([../hosts/web.md](../hosts/web.md)). |
| Migrations | yes | no | Generic host, not a web host; maps no endpoints ([../hosts/migrations.md](../hosts/migrations.md)). |

- Aspire supplies `OTEL_EXPORTER_OTLP_ENDPOINT` to the projects it starts, so the telemetry of the API, Web, and Migrations hosts reaches the Aspire dashboard when the AppHost runs. No resource references a project resource, so no host receives service-discovery entries.
- No application code creates its own `ActivitySource` or `Meter`, and no health check exists beyond `self` and those integrations add.
- The `ChronicleOfHerosDbContext` registration keeps the Aspire Npgsql integration's retrying execution strategy ([../capabilities/identity/persistence.md](../capabilities/identity/persistence.md)).
