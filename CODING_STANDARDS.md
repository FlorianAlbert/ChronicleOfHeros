# Coding standards

These standards govern new and changed code. They describe the intended design,
not a guarantee that every existing implementation already conforms. Before
designing or changing code, read every relevant section and ADR. Surface
conflicts with an ADR rather than silently overriding it.

## Architecture governance

Key words follow RFC 2119. This section defines the rules authors and reviewers
apply; the
[`architecture-planning` skill](.agents/skills/architecture-planning/SKILL.md)
holds the planning procedure.

### Architecture documentation

- `ARCHITECTURE.md` and its slices under `docs/architecture/` are normative and
  MUST describe the code at every commit. Load `ARCHITECTURE.md`, then only the
  slices its reading guide selects.
- [`docs/architecture/conventions.md`](docs/architecture/conventions.md) defines
  diagram scope and level of detail, slice structure, and verified-view syntax.
  Every slice MUST follow it.
- Code that knowingly departs from an ADR or these standards MUST be listed in
  the affected slice's `## Known deviations` section, naming the ADR or standard
  and linking the tracking issue.

### Architectural changes

A change is architectural when it would make a statement or diagram in
`ARCHITECTURE.md`, a slice, these standards, or an ADR inaccurate, or when it
adds something `docs/architecture/conventions.md` requires those documents to
show. This explicitly includes changes to:

- projects and project references;
- production NuGet packages (test and analyzer packages are excluded);
- contract interfaces and their operations;
- HTTP endpoints;
- Aspire resources and parameters;
- `DbContext` types, entities, relationships, and migrations;
- cross-layer or cross-capability collaborations;
- state machines;
- key classes documented in a slice.

Any other change takes the fast path: the author states
`Architecture impact: none (checked slices: …)` and proceeds without approval.
If impact appears mid-task, work MUST stop and return to planning.

### Approval and pending deltas

- An architectural change MUST NOT be implemented before the user answers "yes"
  to an explicit yes/no question approving its documented delta. Nothing else
  counts as approval. The approval is recorded as
  `Approved by @<user> on <YYYY-MM-DD>`. If implementation forces a departure
  from the approved delta, work MUST stop for re-planning and renewed approval.
- Docs equal the code at every commit. An approved change that is not yet built
  lives in its spec or ticket issue's `## Architecture delta` section
  (before/after Mermaid), not in the slices. Each affected slice carries
  `> Pending approved changes: #N` near its top. The implementing change moves
  the delta into the slices and removes the pointer in the same commit as the
  code.
- Non-interactive agents MUST implement only work without architecture impact
  or with an approved `## Architecture delta` in its issue. Otherwise they post
  the proposed delta as an issue or PR comment, apply the
  `needs-architecture-approval` label, and stop.
- Draft highlight classes (`added`, `removed`, `changed`) are planning aids and
  MUST NOT be committed.

### ADRs

- Every ADR has a status line directly under its H1, in one of these forms:
  - `Status: Accepted`: the decision is in force and built.
  - `Status: Accepted, implementation pending (#N)`: the decision is approved
    but not fully built; `#N` is the issue whose `## Architecture delta` holds
    the unbuilt part. Separate several issues with commas: `(#N, #M)`.
  - `Status: Superseded by ADR-NNNN`.
- ADRs are concise living decision records, edited in place. Every ADR change
  goes through the approval gate; the change that completes a pending
  implementation sets its status to `Accepted`.
- Write a new ADR only for a hard-to-reverse or surprising trade-off. Slice
  updates are required for every architectural change, with or without an ADR.

### Verification

- Verified views MUST match the code. The doc-conformance tests in
  `tests/ChronicleOfHeros.Architecture.Tests` check them; those and the other
  architecture tests MUST pass.
- An architecture test that encodes a documented rule changes in the same
  commit as the slice or standard documenting that rule.

### Review checklist

Reviewers MUST apply every item to the reviewed change. Authors MUST apply it to
their own change before completing it.

1. **Code and slices**: every architectural change in the diff appears in the
   affected slices; no slice statement or diagram became inaccurate.
2. **Approval evidence**: every architectural change traces to an
   `Approved by @<user> on <YYYY-MM-DD>` line, either in its issue's
   `## Architecture delta` or in the commit message or PR description, covering
   exactly the implemented delta. An unapproved architectural change is a
   blocking finding.
3. **Pending pointers**: implementing an issue's delta removes its
   `> Pending approved changes: #N` pointers; every remaining pointer names an
   open issue with an approved delta.
4. **Known deviations**: new departures from an ADR or these standards are
   listed under `## Known deviations` with a tracking issue; resolved ones are
   removed.
5. **ADRs**: status lines match the code; ADR edits are approved; a new
   hard-to-reverse or surprising trade-off has an ADR.
6. **Verified views and tests**: verified views match the code; the build and
   architecture tests pass.
7. **Draft residue**: no `added`, `removed`, or `changed` highlight classes
   remain.

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
- Project-reference changes are [architectural changes](#architectural-changes);
  the architecture tests' reference rules change with them.

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
- Before completing any code change, inspect every changed capability boundary,
  composition path, persistence model, database test, and C# extension
  declaration against the applicable standards; the
  [review checklist](#review-checklist) covers the architecture documents.
- Validate changed .NET projects with a build and the smallest relevant test
  selection; expand validation when the change or results require it. Tests
  run on Microsoft.Testing.Platform (see `global.json`), so select a test
  project with `--project`, for example
  `dotnet test --project tests/ChronicleOfHeros.Architecture.Tests/ChronicleOfHeros.Architecture.Tests.csproj`.
  The AppHost and Identity test projects need Docker. When a running app locks
  build outputs, add `--artifacts-path artifacts` (git-ignored; it must stay
  inside the repository because the architecture tests locate the repository
  root from their output directory).
