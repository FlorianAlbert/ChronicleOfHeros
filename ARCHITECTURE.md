# Architecture

ChronicleOfHeros is a digital-first Dungeons & Dragons 5e character management platform. This file routes to the architecture slices under [docs/architecture/](docs/architecture/), which describe the code as it is at this commit.

## Governance

- `ARCHITECTURE.md` and every slice are normative: they MUST describe the current code. A change that would make one of them, an ADR, or `CODING_STANDARDS.md` inaccurate is architectural; the definition is in [CODING_STANDARDS.md § Architecture governance](CODING_STANDARDS.md#architecture-governance).
- Every code-changing task runs [.agents/skills/architecture-planning/SKILL.md](.agents/skills/architecture-planning/SKILL.md): either the fast path `Architecture impact: none (checked slices: …)`, or a drafted delta that the user explicitly approves before implementation.
- An approved but unbuilt change lives in its issue's `## Architecture delta`; each slice it will change carries `> Pending approved changes: #N` under its header.
- Code that departs from an ADR or a standard is listed in its slice under `## Known deviations`, with its tracking issue.
- Verified views (`%% verify:` diagrams and `<!-- verify: -->` tables) are compared with the code in both directions by architecture tests. Slice format, diagram rules, draft highlighting, and the view specification are in [docs/architecture/conventions.md](docs/architecture/conventions.md).

## Reading guide

Load only the slices for your task. Add [conventions.md](docs/architecture/conventions.md) whenever you draft or edit a diagram.

| Task | Slices to load |
|---|---|
| First orientation | [system/context](docs/architecture/system/context.md), [solution/projects](docs/architecture/solution/projects.md), [solution/capabilities](docs/architecture/solution/capabilities.md) |
| Add or change an API endpoint | [hosts/api](docs/architecture/hosts/api.md), the matching flow below, [cross-cutting/security](docs/architecture/cross-cutting/security.md) |
| Sign-in, password change, tokens, refresh, sign-out | [flows/authentication](docs/architecture/flows/authentication.md), [identity/state](docs/architecture/capabilities/identity/state.md), [cross-cutting/security](docs/architecture/cross-cutting/security.md) |
| Player enrollment, password reset, Operator rules | [flows/player-administration](docs/architecture/flows/player-administration.md), [identity/domain](docs/architecture/capabilities/identity/domain.md) |
| Identity service, contract operation, or registration | [identity/classes](docs/architecture/capabilities/identity/classes.md), [identity/components](docs/architecture/capabilities/identity/components.md) |
| Identity entity, `DbContext`, or migration | [identity/persistence](docs/architecture/capabilities/identity/persistence.md), [hosts/migrations](docs/architecture/hosts/migrations.md) |
| Move a type between layers; add a dependency | [identity/components](docs/architecture/capabilities/identity/components.md), [solution/projects](docs/architecture/solution/projects.md) |
| Add a capability, project, or project reference | [solution/capabilities](docs/architecture/solution/capabilities.md), [solution/projects](docs/architecture/solution/projects.md), [identity/components](docs/architecture/capabilities/identity/components.md) as the reference example |
| AppHost resource, parameter, or start order | [system/deployment](docs/architecture/system/deployment.md), [flows/startup](docs/architecture/flows/startup.md) |
| Web page, component, render mode, or Web endpoint | [hosts/web](docs/architecture/hosts/web.md), [cross-cutting/localization](docs/architecture/cross-cutting/localization.md) |
| Browser-to-API path, Web session, BFF work | [hosts/web](docs/architecture/hosts/web.md), [flows/web-forwarding](docs/architecture/flows/web-forwarding.md), [flows/authentication](docs/architecture/flows/authentication.md), [cross-cutting/security](docs/architecture/cross-cutting/security.md), [system/deployment](docs/architecture/system/deployment.md) |
| Display languages or translations | [cross-cutting/localization](docs/architecture/cross-cutting/localization.md) |
| Telemetry, health checks, resilience, service discovery | [cross-cutting/observability](docs/architecture/cross-cutting/observability.md) |
| Draft a delta; write or test architecture docs | [conventions](docs/architecture/conventions.md), then the slices the delta changes |

## Slice index

| Slice | Load when |
|---|---|
| [conventions](docs/architecture/conventions.md) | Writing, drafting, or reviewing a slice or delta; implementing doc-conformance tests. |
| **System** | |
| [system/context](docs/architecture/system/context.md) | Orienting; changing actors, browser entry points, or external dependencies. |
| [system/deployment](docs/architecture/system/deployment.md) | Changing `AppHost.cs`: resources, parameters, references, start order, health checks. |
| **Solution** | |
| [solution/projects](docs/architecture/solution/projects.md) | Adding a project, project reference, or production package. |
| [solution/capabilities](docs/architecture/solution/capabilities.md) | Adding a capability or wiring one into a host. |
| **Identity capability** ([sub-index](docs/architecture/capabilities/identity/index.md)) | |
| [identity/components](docs/architecture/capabilities/identity/components.md) | Moving a type between layers or namespaces; adding a layer dependency. |
| [identity/classes](docs/architecture/capabilities/identity/classes.md) | Changing a contract operation, service, token handling, registration, or hosted service. |
| [identity/domain](docs/architecture/capabilities/identity/domain.md) | Changing roles, Username rules, credentials, password rules, or account state. |
| [identity/persistence](docs/architecture/capabilities/identity/persistence.md) | Changing an entity, the `DbContext`, a configuration, or a migration. |
| [identity/state](docs/architecture/capabilities/identity/state.md) | Changing the must-change-password rule or the refresh-session lifecycle. |
| **Hosts** | |
| [hosts/api](docs/architecture/hosts/api.md) | Changing an API endpoint, its authorization or results, or API composition. |
| [hosts/web](docs/architecture/hosts/web.md) | Changing the Web pipeline, pages, components, render modes, or Web endpoints. |
| [hosts/migrations](docs/architecture/hosts/migrations.md) | Changing how or when schema migrations run. |
| **Flows** | |
| [flows/authentication](docs/architecture/flows/authentication.md) | Changing sign-in, password change, refresh rotation, or sign-out. |
| [flows/player-administration](docs/architecture/flows/player-administration.md) | Changing enrollment, password reset, or the current-Player lookup. |
| [flows/web-forwarding](docs/architecture/flows/web-forwarding.md) | Changing how browser requests reach the API. |
| [flows/startup](docs/architecture/flows/startup.md) | Changing start order, startup migration, startup validation, or the bootstrap Operator. |
| **Cross-cutting** | |
| [cross-cutting/localization](docs/architecture/cross-cutting/localization.md) | Changing display-language selection, the selector, resources, or fallback. |
| [cross-cutting/security](docs/architecture/cross-cutting/security.md) | Changing JWTs, keys, policies, password rules, cookies, antiforgery, or HTTPS. |
| [cross-cutting/observability](docs/architecture/cross-cutting/observability.md) | Changing service defaults, telemetry, health endpoints, or resilience. |
