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
`dotnet test`.

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
change. Run the entire test project before reporting a broad change complete.

## Coverage

When the test project references `Microsoft.Testing.Extensions.CodeCoverage`,
collect coverage with:

```bash
dotnet test --project <test-project>.csproj --coverage
```

## Runtime Dependencies

Tests that host distributed application resources need their configured
container runtime available. Browser tests using Microsoft Playwright also need
the required browser binaries installed. Treat setup failures from either
dependency as environment blockers, separate from test failures.