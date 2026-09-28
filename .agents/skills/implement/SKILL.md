---
name: implement
description: "Implement a piece of work based on a spec or set of tickets."
disable-model-invocation: true
---

Implement the work described by the user in the spec or tickets.

Use /tdd where possible, at pre-agreed seams.

For capability boundaries, dependency injection, persistence, migrations, database tests, or C# extensions, read `docs/agents/architecture.md` before editing.

Validate changed .NET projects with `dotnet build` and run the smallest relevant existing test selection through the `run-tests` skill. Expand validation when targeted results or the change's scope require it. This repo uses xUnit v3 through Microsoft Testing Platform, not a JavaScript test runner or VSTest filters.

Once done, use /code-review to review the work.

Commit your work to the current branch.
