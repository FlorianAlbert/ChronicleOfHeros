# Hosts: API

Scope: the `ChronicleOfHeros.Api` host: composition, request pipeline, and HTTP endpoints.
Load when: adding an API endpoint, authentication or authorization, or changing the API host's composition.
Parent: [ARCHITECTURE.md](../../../ARCHITECTURE.md) · Neighbours: [web.md](web.md), [../cross-cutting/security.md](../cross-cutting/security.md)

## Composition and pipeline

`Program.cs` (top-level statements), in order:

1. `AddServiceDefaults()` ([../cross-cutting/observability.md](../cross-cutting/observability.md)).
2. `AddAuthentication().AddKeycloakJwtBearer("keycloak", "chronicleofheroes")` with authority `https://keycloak/realms/chronicleofheroes` and audience `chronicleofheroes-api`; `AddAuthorizationBuilder()` without policies.
3. Build the application.
4. `UseHttpsRedirection()`, `UseAuthentication()`, `UseAuthorization()`.
5. `MapDefaultEndpoints()`.

The API has no application endpoints or database integration, so no endpoint requires a bearer token yet. It has no exception handler, CORS, or antiforgery middleware; configuration comes from the AppHost ([../system/deployment.md](../system/deployment.md)).

## Endpoints

<!-- verify: api-endpoints -->
| Method | Route | Adapter | Authorization |
|---|---|---|---|

There are no application endpoints. `MapDefaultEndpoints()` exposes `/health` and `/alive` only in Development ([../cross-cutting/observability.md](../cross-cutting/observability.md)).
