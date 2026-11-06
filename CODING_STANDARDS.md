# Coding standards

These standards govern new and changed code. They describe the intended design,
not a guarantee that every existing implementation already conforms. Before
designing or changing code, read every relevant section and ADR. Surface
conflicts with an ADR rather than silently overriding it.

## Capability architecture

### Contracts and ownership

- Model each independently composable product concern as a capability with
  exactly one public contracts project and one implementation project.
- Contracts contain public interfaces and transport-neutral request and response
  DTOs, organized by public responsibility. Keep cross-boundary operation
  results in their own contract. Do not expose implementations, persistence
  entities, database contexts, framework options, or framework identity types.
- A capability owns its domain concepts and relational data, including its
  context, entities, configurations, and migrations.
- Keep contracts unversioned while all consumers are inside the pre-release
  solution. Introduce versioning when an independently deployed external
  consumer requires it.
- Consumers and transport adapters reference another capability only through
  its contracts. Executable hosts and the implementation's own tests may
  reference the implementation only to call its public registration API.

### Implementation layers

- Keep `Domain`, `Application`, `Infrastructure`, and `DependencyInjection`
  inside the implementation project.
- `Domain` has no framework or other internal-layer dependencies.
  `Application` depends only on `Domain`, public contracts, and private ports
  it owns. `Infrastructure` implements those ports and owns persistence and
  framework integrations. `DependencyInjection` alone assembles the capability.
- Keep implementations and private ports internal. Do not expose internals to
  tests with `InternalsVisibleTo`. Public implementation surface is limited to
  the registration API, narrowly necessary composition configuration types, and
  approved framework-convention types; public behavior belongs in contracts.
- Introduce a shared kernel only for a stable business concept with genuine
  shared ownership, an ADR, and a narrow dependency allowlist. Framework-surface
  exceptions likewise require an ADR and a narrow architecture-test allowlist
  explaining the exception and its allowed consumers.

### Registration and composition

- The host calls exactly one registration method per installed capability.
  Put alternatives in its `Action<TOptions>` callback, using explicit option
  methods such as `options => options.EnableMigrations()`.
- Every service-registration configuration callback calls an explicitly named
  member on its options object. Registration owns configuration binding and
  validation; required invalid configuration fails at startup. Raw configuration
  and the implementation's service graph are not public APIs.
- Hosts are composition and transport adapters: entry points configure the host,
  register capabilities, and compose endpoints rather than own business workflows
  or framework persistence.
- Each HTTP endpoint adapter owns one route-and-method operation, including
  authorization, request binding, and response mapping, and depends on
  capability contracts. Register and map adapters explicitly through dependency
  injection, without reflection scanning.
- A dedicated migrations host enables migrations through capability registration
  options. Normal application and distributed-application startup do not run
  migrations.
- Treat project-reference changes as architectural changes. Update architecture
  tests and record any long-lived boundary decision in these standards or an ADR.

### Persistence

- Use PostgreSQL with EF Core and Npgsql for capability-owned relational data.
  Registration uses Aspire's PostgreSQL integration for service discovery and
  PostgreSQL-aware health checks.
- Every concrete `DbContext` overrides `OnModelCreating` and explicitly applies
  every entity configuration it owns. Put each mapping in a separate
  `IEntityTypeConfiguration<TEntity>` and apply known configurations directly,
  without assembly scanning.

See [ADR 0002](docs/adr/0002-postgresql-as-the-datastore.md) and
[ADR 0007](docs/adr/0007-contract-first-capability-architecture.md).

## C# style

- Follow the repository's [`.editorconfig`](.editorconfig) for formatting and
  style settings.
- Write LINQ with fluent method chains such as `.Where(...).Select(...)`, rather
  than C# query-expression syntax.
- Extension containers represent adapter behavior and use the `Extensions`
  suffix; runtime services do not use that suffix. Declare extension methods
  and members with C# 14+ `extension(...)` blocks, not legacy `this` parameters.
- Prefer MSBuild properties or items to source assembly attributes for assembly
  metadata or visibility configuration.

## Web and security boundaries

- Orchestrate the distributed application with Aspire and use shared service
  defaults for telemetry, resilience, and health configuration.
- Render Blazor statically by default. A component that needs Blazor
  interactivity opts into `InteractiveServer`.
- The Web host is the sole browser-facing origin and acts as a backend for
  frontend (BFF); the API remains an internal JWT-bearer API. Expose explicit,
  capability-owned browser adapters rather than a blanket API forwarder.
- Keep API tokens server-side. Browser authentication uses an opaque Web session
  cookie; browser mutations require antiforgery validation. Follow the session,
  token custody, authorization, refresh, and sign-out requirements in
  [ADR 0004](docs/adr/0004-same-origin-web-api-gateway.md) and
  [ADR 0006](docs/adr/0006-identity-backed-jwt-authentication.md).

See also [ADR 0001](docs/adr/0001-aspire-orchestrated-distributed-architecture.md)
and [ADR 0003](docs/adr/0003-blazor-auto-render-mode.md).

## Localization

- Localize application-owned visible Web text, page titles, and accessibility
  labels. Each page or reusable component owns resources beside its
  implementation; reserve shared resources for genuinely cross-cutting
  vocabulary.
- Use the neutral English resource as the canonical key set. Every supported
  display language has a resource with a concrete locale suffix and the same
  keys; choose descriptive keys for the owning component's meaning. Use
  `IStringLocalizer<TComponent>` for component resources and the shared-resource
  localizer only for shared vocabulary.
- Missing localized values fall back to English; if the English key is also
  missing, display the key and record a warning. Treat either omission as a
  translation defect. Test fallback with test-only incomplete resources, keeping
  production resources complete.
- When adding a display language, register its concrete locale and formatting
  culture, provide matching resources, expose its self-name in the selector,
  and ensure the resource-parity tests cover it. Add browser and selector
  coverage for the new language.
- German copy uses informal singular `du` and official German Dungeons & Dragons
  5e terminology where available. Keep English proper names only when there is
  no official German term.
- Display culture affects formatting, not Dungeons & Dragons rules units or
  calculations. Run resource-parity tests and cover relevant browser-visible
  behavior when changing translations or supported languages.

See [GLOSSARY.md](GLOSSARY.md) for display-language behavior and
[ADR 0005](docs/adr/0005-localization-architecture.md) for the localization
architecture decision.

## Tests and verification

- Test capability behavior through public contracts and registration: resolve
  public contract interfaces and assert observable results using contract DTOs.
- Test relational behavior against PostgreSQL in Testcontainers. Apply the
  capability's migrations through its migration registration option before
  constructing runtime registration. PGLite, EF Core InMemory, SQLite, mocked
  databases, and mocked internal collaborators are not substitutes.
- Use distributed-application tests for cross-host behavior such as gateway,
  health, and authentication flows, retaining the owned collaborators. Test
  browser-visible behavior with C# Playwright or an Aspire integration test
  rather than mocking a component's own services. Use xUnit v3 for .NET tests
  and `Aspire.Hosting.Testing` for distributed-application tests.
- Architecture tests are build-breaking and cover reference direction, internal
  layers, public surfaces, composition roots, and endpoint rules; review is
  still responsible for cohesion and single-responsibility judgment.
- Before completing architecture work, inspect every changed capability boundary,
  composition path, persistence model, database test, and C# extension
  declaration against the applicable standards.
- Validate changed .NET projects with a build and the smallest relevant test
  selection; expand validation when the change or results require it. Use the
  [`run-tests` skill](.agents/skills/run-tests/SKILL.md) for test execution.
