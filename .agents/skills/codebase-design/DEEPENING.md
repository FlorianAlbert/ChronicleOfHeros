# Deepening

How to deepen a cluster of shallow modules safely, given its dependencies. Assumes the vocabulary in [SKILL.md](SKILL.md): **module**, **interface**, **seam**, **adapter**.

Read `docs/agents/architecture.md` for capability and persistence work. Preserve the required contracts project, internal implementation layers, and public registration API when deepening a capability.

## Dependency categories

When assessing a candidate for deepening, classify its dependencies. The category determines how the deepened module is tested across its seam.

### 1. In-process

Pure computation, in-memory state, no I/O. Always deepenable: merge the modules and test through the new interface directly. No adapter needed.

### 2. Local infrastructure

For relational behavior, run real PostgreSQL through Testcontainers and apply migrations through the capability's migration registration option before constructing runtime registration. Do not substitute PGLite, EF Core InMemory, SQLite, or a mocked database. Test behavior through public contracts; persistence and its ports stay internal. Other system dependencies may use stand-ins only when they preserve the behavior under test.

### 3. Remote but owned (Ports & Adapters)

Your own hosts across a network boundary (the Web gateway and API). Endpoint adapters depend on capability contracts; transport and persistence do not leak into those contracts. Exercise cross-host behavior with `Aspire.Hosting.Testing`, using C# Playwright when browser-visible behavior is the seam. Do not replace the owned collaborators with mocks or in-memory adapters in those tests.

Recommendation shape: *"Keep the capability's public contracts narrow and its implementation internal; verify the gateway/API flow through the distributed application."*

### 4. True external (Mock)

Third-party services (Stripe, Twilio, etc.) you don't control. The deepened module takes the external dependency as an injected port; tests provide a mock adapter.

## Seam discipline

- **One adapter means a hypothetical seam. Two adapters means a real one.** Don't introduce optional ports solely to make tests easier. This does not remove the capability's required contracts or registration API.
- **Internal seams vs external seams.** Keep private ports internal. Capability tests enter implementations only through registration, resolve public contract interfaces, and assert contract DTOs. Don't expose internal seams or add `InternalsVisibleTo` for tests.

## Testing strategy: replace, don't layer

- Replace implementation-coupled unit tests only after equivalent behavior is verified at the deepened module's public interface. Preserve architecture tests and distinct distributed/browser coverage.
- Write new tests at the deepened module's interface. The **interface is the test surface**.
- Tests assert on observable outcomes through the interface, not internal state.
- Tests should survive internal refactors, since they describe behaviour, not implementation. If a test has to change when the implementation changes, it's testing past the interface.

Use xUnit v3 and the `run-tests` skill for Microsoft Testing Platform execution.
