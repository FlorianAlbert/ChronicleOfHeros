# Architecture

ChronicleOfHeros is a digital-first Dungeons & Dragons 5e character management platform. This file routes to the architecture slices under [docs/architecture/](docs/architecture/), which describe the code as it is at this commit.

## Governance

- `ARCHITECTURE.md` and every slice are normative: they MUST describe the current code. A change that would make one of them, an ADR, or `CODING_STANDARDS.md` inaccurate is architectural; the definition is in [CODING_STANDARDS.md § Architecture governance](CODING_STANDARDS.md#architecture-governance).
- Every code-changing task runs [.agents/skills/architecture-planning/SKILL.md](.agents/skills/architecture-planning/SKILL.md): either the fast path `Architecture impact: none (checked slices: …)`, or a contribution covered by explicit architecture approval.
- Planning progresses through grilling/wayfinding, spec creation, dedicated architecture design, and ticket creation. The target lives in the spec issue's `## Architecture design`; tickets reference it and describe their own transitions. Small direct tasks may use a compact approved conversation proposal.
- Current-state slices contain only implemented reality, without target drafts or pending-design pointers. Discover related designs through the [issue tracker guide](docs/agents/issue-tracker.md#architecture-design). Recheck relevant assumptions before implementation; no pinned baseline commit is required.
- Code that departs from an ADR or a standard is listed in its slice under `## Known deviations`, with its tracking issue.
- Verified views (`%% verify:` diagrams and `<!-- verify: -->` tables) are compared with the code in both directions by architecture tests. Slice format, diagram rules, planning-view separation, and the view specification are in [docs/architecture/conventions.md](docs/architecture/conventions.md).

## Reading guide

Load only the slices for your task. Add [conventions.md](docs/architecture/conventions.md) whenever you draft or edit a diagram.

| Task | Slices to load |
|---|---|
| First orientation | [system/context](docs/architecture/system/context.md), [solution/projects](docs/architecture/solution/projects.md), [solution/capabilities](docs/architecture/solution/capabilities.md) |
| Add or change an API endpoint | [hosts/api](docs/architecture/hosts/api.md), the matching flow below, [cross-cutting/security](docs/architecture/cross-cutting/security.md) |
| API composition or endpoint | [hosts/api](docs/architecture/hosts/api.md), [cross-cutting/security](docs/architecture/cross-cutting/security.md) |
| Add a capability, project, or project reference | [solution/capabilities](docs/architecture/solution/capabilities.md), [solution/projects](docs/architecture/solution/projects.md) |
| AppHost resource, parameter, or start order | [system/deployment](docs/architecture/system/deployment.md), [flows/startup](docs/architecture/flows/startup.md) |
| Web page, component, render mode, or Web endpoint | [hosts/web](docs/architecture/hosts/web.md), [cross-cutting/localization](docs/architecture/cross-cutting/localization.md) |
| Browser-to-API path | [hosts/web](docs/architecture/hosts/web.md), [hosts/api](docs/architecture/hosts/api.md), [system/deployment](docs/architecture/system/deployment.md) |
| Display languages or translations | [cross-cutting/localization](docs/architecture/cross-cutting/localization.md) |
| Telemetry, health checks, resilience, service discovery | [cross-cutting/observability](docs/architecture/cross-cutting/observability.md) |
| Plan a design; write or test architecture docs | [conventions](docs/architecture/conventions.md), then the affected slices |

## Slice index

| Slice | Load when |
|---|---|
| [conventions](docs/architecture/conventions.md) | Writing or reviewing a slice or target design; implementing doc-conformance tests. |
| **System** | |
| [system/context](docs/architecture/system/context.md) | Orienting; changing actors, browser entry points, or external dependencies. |
| [system/deployment](docs/architecture/system/deployment.md) | Changing `AppHost.cs`: resources, parameters, references, start order, health checks. |
| **Solution** | |
| [solution/projects](docs/architecture/solution/projects.md) | Adding a project, project reference, or production package. |
| [solution/capabilities](docs/architecture/solution/capabilities.md) | Adding a capability or wiring one into a host. |
| **Hosts** | |
| [hosts/api](docs/architecture/hosts/api.md) | Changing API composition, endpoints, authentication, or authorization. |
| [hosts/web](docs/architecture/hosts/web.md) | Changing the Web pipeline, pages, components, render modes, or Web endpoints. |
| **Flows** | |
| [flows/startup](docs/architecture/flows/startup.md) | Changing start order or resource readiness. |
| **Cross-cutting** | |
| [cross-cutting/localization](docs/architecture/cross-cutting/localization.md) | Changing display-language selection, the selector, resources, or fallback. |
| [cross-cutting/security](docs/architecture/cross-cutting/security.md) | Adding authentication or authorization, or changing cookies, antiforgery, or HTTPS. |
| [cross-cutting/observability](docs/architecture/cross-cutting/observability.md) | Changing service defaults, telemetry, health endpoints, or resilience. |
