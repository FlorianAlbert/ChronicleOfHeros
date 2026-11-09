# System: deployment

Scope: the Aspire AppHost resource graph: resources, parameters, references, start order, and health checks.
Load when: changing `src/ChronicleOfHeros.AppHost/AppHost.cs`, adding a resource or parameter, or changing start order or configuration flow between hosts.
Parent: [ARCHITECTURE.md](../../../ARCHITECTURE.md) · Neighbours: [context.md](context.md), [../flows/startup.md](../flows/startup.md), [../cross-cutting/observability.md](../cross-cutting/observability.md)

`ChronicleOfHeros.AppHost` (Aspire AppHost SDK 13.6.4, packages `Aspire.Hosting.Dotnet` and `Aspire.Hosting.PostgreSQL`) declares the graph in `AppHost.cs`. It has no project references; it adds projects by path with `AddDotnetProject`.

```mermaid
flowchart LR
    postgres[("postgres<br/>PostgreSQL container")]
    database[("chronicleofheros<br/>database")]
    migrations["migrations<br/>ChronicleOfHeros.Migrations"]
    api["api<br/>ChronicleOfHeros.Api"]
    web["web<br/>ChronicleOfHeros.Web"]
    params[/"parameters<br/>bootstrap-operator-*, jwt-*"/]
    postgres -->|"parent of"| database
    migrations -->|"reference, waits for"| database
    api -->|"reference"| database
    api -->|"waits for completion"| migrations
    web -->|"reference, waits for"| api
    params -->|"environment variables"| api
```

## Resources

<!-- verify: aspire-resources -->
| Resource | Declared by | Parent | Project | References | Waits for | Health check |
|---|---|---|---|---|---|---|
| postgres | AddPostgres | - | - | - | - | - |
| chronicleofheros | AddDatabase | postgres | - | - | - | - |
| migrations | AddDotnetProject | - | ChronicleOfHeros.Migrations | chronicleofheros | chronicleofheros | - |
| api | AddDotnetProject | - | ChronicleOfHeros.Api | chronicleofheros | migrations (completion) | http /health |
| web | AddDotnetProject | - | ChronicleOfHeros.Web | api | api | - |

- `WithReference(database)` gives the host the connection string `ConnectionStrings:chronicleofheros`; Identity registration reads it by that name ([../capabilities/identity/persistence.md](../capabilities/identity/persistence.md)).
- `WithReference(api)` gives Web the service-discovery entries for `api` over its single `https` endpoint. Web holds the reference for the future BFF (#84) but does not use the entries yet ([../hosts/web.md](../hosts/web.md)).
- The API and Web launch profiles declare only `https` URLs, so each of these project resources has a single `https` endpoint and no plain-HTTP endpoint. The API health check polls `/health` on that endpoint.
- The API starts only after the migrations resource has exited successfully; Web starts after the API is healthy. The API health check polls `/health`, which exists only in the Development environment ([../cross-cutting/observability.md](../cross-cutting/observability.md)).
- The PostgreSQL integration adds its own password parameter and health check; they are not declared in `AppHost.cs`.
- No resource declares external HTTP endpoints, persistent lifetime, or data volumes.

## Parameters

<!-- verify: aspire-parameters -->
| Parameter | Secret | Consumed as |
|---|---|---|
| bootstrap-operator-username | no | api:BootstrapOperator__Username |
| bootstrap-operator-temporary-password | yes | api:BootstrapOperator__TemporaryPassword |
| jwt-signing-private-key | yes | api:Jwt__SigningPrivateKey |
| jwt-issuer | no | api:Jwt__Issuer |
| jwt-audience | no | api:Jwt__Audience |

- Values come from AppHost configuration (`Parameters:<name>`, e.g. user secrets; the project has a `UserSecretsId` and no `appsettings` file).
- The API binds them as `BootstrapOperator:*` and `Jwt:*` configuration ([../cross-cutting/security.md](../cross-cutting/security.md)). `jwt-signing-private-key` is a Base64 PKCS#8 RSA private key.
- Only the API receives parameters. Web and Migrations receive none.

## Known deviations

- Web receives no public signing key, and there is no Redis or Data Protection key resource. Deviates from [ADR 0004](../../adr/0004-same-origin-web-api-gateway.md): Web keeps its session in private Redis, protects token values with Data Protection, and validates its server-held access JWT with the API-provisioned public signing key. Tracking: #84.
