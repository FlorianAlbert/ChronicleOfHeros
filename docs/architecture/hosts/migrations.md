# Hosts: Migrations

Scope: the `ChronicleOfHeros.Migrations` host, which applies capability migrations and exits.
Load when: changing how or when schema migrations run, or adding a capability with its own migrations.
Parent: [ARCHITECTURE.md](../../../ARCHITECTURE.md) · Neighbours: [api.md](api.md), [web.md](web.md), [../capabilities/identity/persistence.md](../capabilities/identity/persistence.md), [../flows/startup.md](../flows/startup.md)

`ChronicleOfHeros.Migrations` is a console executable (`Microsoft.NET.Sdk`, `OutputType` `Exe`) whose only project reference is `ChronicleOfHeros.Identity.AspNetCore`. `Program.cs`:

1. `Host.CreateApplicationBuilder(args)`.
2. `AddAspNetCoreIdentity(options => options.EnableMigrations())`: registers only `ChronicleOfHerosDbContext` and `IdentityMigrationHostedService` ([../capabilities/identity/classes.md](../capabilities/identity/classes.md)).
3. `RunAsync()`. The hosted service applies pending migrations with `Database.MigrateAsync` and then calls `StopApplication()`, so the process exits with code 0. A failed migration fails host start and the process exits with a non-zero code.

- The connection string comes from the AppHost reference to the `chronicleofheros` database ([../system/deployment.md](../system/deployment.md)).
- The AppHost starts this host on every run and starts the API only after it completes successfully ([../flows/startup.md](../flows/startup.md)).
- It does not call `AddServiceDefaults()`, so the host configures no OTLP exporter, service discovery, standard HTTP resilience, or health endpoints.

## Known deviations

- The AppHost runs the Migrations host, and therefore applies migrations, on every distributed-application start. Deviates from [CODING_STANDARDS.md § Registration and composition](../../../CODING_STANDARDS.md#registration-and-composition): normal application and distributed-application startup do not run migrations. Tracking: not yet filed.
- The host does not use the shared service defaults. Deviates from [CODING_STANDARDS.md § Web and security boundaries](../../../CODING_STANDARDS.md#web-and-security-boundaries) and [ADR 0001](../../adr/0001-aspire-orchestrated-distributed-architecture.md): shared service defaults supply telemetry, resilience, and health configuration. Tracking: not yet filed.
