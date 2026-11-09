# System: context

Scope: who uses ChronicleOfHeros and which runtime parts and external systems exist today.
Load when: orienting in the system; changing actors, browser entry points, or external dependencies.
Parent: [ARCHITECTURE.md](../../../ARCHITECTURE.md) · Neighbours: [deployment.md](deployment.md), [../solution/projects.md](../solution/projects.md)

> Pending approved changes: #75

```mermaid
flowchart LR
    player["Player or Operator<br/>(browser or HTTP client)"]
    web["ChronicleOfHeros.Web<br/>Blazor pages"]
    api["ChronicleOfHeros.Api<br/>JWT-bearer Minimal API"]
    migrations["ChronicleOfHeros.Migrations<br/>run-once schema migrator"]
    db[("PostgreSQL<br/>database chronicleofheros")]
    player -->|"HTTPS pages, POST /display-language"| web
    player -->|"HTTPS JSON with own bearer token"| api
    api -->|"EF Core / Npgsql"| db
    migrations -->|"EF Core migrations"| db
```

- **Actors.** A [Player](../../../GLOSSARY.md#player) and an [Operator](../../../GLOSSARY.md#operator) (a Player with the Operator role) are the only actors. Non-human API clients and DM actors are out of scope.
- **Web** is the only host meant for browsers. It renders the Blazor pages and has no route to the API and makes no API calls ([hosts/web.md](../hosts/web.md)).
- **API** owns authentication and Player administration over JSON on HTTPS. HTTP clients call it directly; callers obtain JWT access and refresh tokens from `/authentication/*` and hold them themselves; no Web page signs in yet ([hosts/api.md](../hosts/api.md)).
- **PostgreSQL** holds only Identity data. No character data exists yet.
- **Migrations** applies the Identity schema and exits ([hosts/migrations.md](../hosts/migrations.md)).
- There are no other external systems: no cache, no mail, no external identity provider.
- [Aspire AppHost](deployment.md) orchestrates every part for local development; no production deployment target is defined.

## Known deviations

- Browser-facing Web has no Web session and no browser adapters for API operations; API callers handle API tokens themselves. Deviates from [ADR 0004](../../adr/0004-same-origin-web-api-gateway.md): Web is a BFF with a Redis session, explicit adapters, and server-held tokens. Tracking: #76.
