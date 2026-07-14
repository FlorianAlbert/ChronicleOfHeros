---
name: implement
description: "Implement a piece of work based on a spec or set of tickets."
disable-model-invocation: true
---

Make sure to work on a dedicated branch for the work you are implementing. If you are not already on a dedicated branch, create one and switch to it. If you are already on a dedicated branch, make sure it is up to date with the main branch.

Implement the work described by the user in the spec or tickets.

Use /tdd where possible, at pre-agreed seams.

Run typechecking regularly, single test files regularly, and the full test suite once at the end.

Once done, use /code-review to review the work.

Add your work to the staging area, but do not commit to the current branch.
