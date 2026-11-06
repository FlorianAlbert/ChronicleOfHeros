# Cross-cutting: observability and resilience

Scope: shared service defaults: OpenTelemetry, health checks, service discovery, and HTTP resilience, and which hosts use them.
Load when: changing `ChronicleOfHeros.ServiceDefaults`, telemetry, health endpoints, service discovery, or HTTP client resilience.
Parent: [ARCHITECTURE.md](../../../ARCHITECTURE.md) · Neighbours: [localization.md](localization.md), [security.md](security.md), [../system/deployment.md](../system/deployment.md)

`ChronicleOfHeros.ServiceDefaults` is an Aspire shared project (`IsAspireSharedProject`) with one public extension container, `ServiceDefaultsExtensions` in `Extensions.cs`, declared in namespace `Microsoft.Extensions.Hosting`.

| Member | Effect |
|---|---|
| `AddServiceDefaults()` | `ConfigureOpenTelemetry()`, `AddDefaultHealthChecks()`, `AddServiceDiscovery()`, and HTTP client defaults: standard resilience handler plus service discovery on every `IHttpClientFactory` client |
| `ConfigureOpenTelemetry()` | Logs with formatted messages and scopes; metrics for ASP.NET Core, `HttpClient`, and runtime; traces for the application's own `ActivitySource`, ASP.NET Core (excluding `/health` and `/alive`), and `HttpClient`. Adds the OTLP exporter only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set |
| `AddDefaultHealthChecks()` | Health check `self`, always healthy, tagged `live` |
| `MapDefaultEndpoints()` | Development only: `/health` runs all checks; `/alive` runs checks tagged `live`. Elsewhere it maps nothing |

| Host | `AddServiceDefaults` | `MapDefaultEndpoints` | Notes |
|---|---|---|---|
| API | yes | yes | The Aspire Npgsql integration adds a database health check to `/health`. The AppHost polls `/health` ([../system/deployment.md](../system/deployment.md)). |
| Web | yes | yes | The forwarder's named resilience options disable retries ([../hosts/web.md](../hosts/web.md)). |
| Migrations | no | — | Not an HTTP host ([../hosts/migrations.md](../hosts/migrations.md)). |

- Aspire supplies `OTEL_EXPORTER_OTLP_ENDPOINT` and service-discovery configuration to the projects it starts, so their telemetry reaches the Aspire dashboard when the AppHost runs.
- No application code creates its own `ActivitySource` or `Meter`, and no health check exists beyond `self` and those integrations add.
- The `ChronicleOfHerosDbContext` registration keeps the Aspire Npgsql integration's retrying execution strategy ([../capabilities/identity/persistence.md](../capabilities/identity/persistence.md)).

## Known deviations

- `AddServiceDefaults` and `ConfigureOpenTelemetry` configure through inline multi-statement and property-assigning callbacks (`Extensions.cs` lines 34–38, 49–53, 62–64). Deviates from [CODING_STANDARDS.md § Registration and composition](../../../CODING_STANDARDS.md#registration-and-composition): every service-registration configuration callback calls an explicitly named member on its options object. Tracking: not yet filed.
- The Migrations host does not use the shared service defaults. Deviates from [CODING_STANDARDS.md § Web and security boundaries](../../../CODING_STANDARDS.md#web-and-security-boundaries): shared service defaults supply telemetry, resilience, and health configuration. Tracking: not yet filed.
