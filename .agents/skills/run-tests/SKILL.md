---
name: run-tests
description: Run xUnit v3 tests through Microsoft Testing Platform. Use when executing, listing, narrowing, or collecting coverage for .NET tests with dotnet test.
---

# Run xUnit Tests

Use `dotnet test` with an explicit test project:

```bash
dotnet test --project <test-project>.csproj
```

The project must opt into Microsoft Testing Platform's `dotnet test` support
and use its runner. Pass Microsoft Testing Platform options directly to
`dotnet test`, not VSTest options such as `--filter`.

## Focused Runs

List discovered tests before selecting a narrow target:

```bash
dotnet test --project <test-project>.csproj --list-tests
```

Run a single xUnit test class by its fully qualified name:

```bash
dotnet test --project <test-project>.csproj --filter-class <fully-qualified-test-class>
```

Use the narrowest relevant class filter for the first validation after a code
change. Combine related selectors in one invocation when the runner supports it.
Expand to the full test project only when targeted results or the change's scope
require it.

## Execution Lifecycle

Use the `terminal-await` skill for every test invocation, including long-running
test projects. When a test run defers, retain its execution ID and resume from
its completion notification; do not rerun the command or poll for completion.

## Coverage

When the test project references `Microsoft.Testing.Extensions.CodeCoverage`,
collect coverage with:

```bash
dotnet test --project <test-project>.csproj --coverage
```

## Runtime Dependencies

Tests that host distributed application resources or PostgreSQL Testcontainers
need their configured container runtime available. Browser tests using C#
Playwright also need the required browser binaries installed. Treat setup
failures from either dependency as environment blockers, separate from test
failures.
