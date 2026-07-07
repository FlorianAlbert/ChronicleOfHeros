# Copilot Instructions — ChronicleOfHeros

## Project Context — Always Assume

- ChronicleOfHeros is a **digital-first Dungeons & Dragons 5e** character management platform intended to replace paper character sheets.
- The platform is the **single source of truth** for character data, derived stats, and progression state.
- The current product focus is **character management** (not campaign/DM tooling as primary scope).
- Prioritize **rules correctness and UX equally** when proposing or implementing changes.
- Character calculations must be **automatic and explainable**: derived values should be traceable to their underlying inputs and rules.
- Use **official D&D 5e rules only**. Homebrew/custom rules are explicitly **out of scope for now**.

## OpenSpec Workflow — Mandatory

**All feature work MUST go through the OpenSpec spec-driven workflow. No exceptions.**  
Never implement a feature outside of an active OpenSpec change. The available skills (`openspec-propose`, `openspec-apply-change`, `openspec-verify-change`, `openspec-archive-change`, `openspec-sync-specs`, `openspec-explore`) contain the full operational details — invoke them instead of improvising.

## Requirement Discussions — Always Grill

Whenever a requirement is being discussed or developed, invoke the `grilling` skill to stress-test it before it gets written into any artifact.

**Critical sequencing rule:** Before invoking `openspec-explore` or `openspec-propose`, the agent MUST invoke `grilling` first.
Do not run `openspec-explore` or `openspec-propose` unless a `grilling` pass has already happened for that requirement in the current workflow.

## Test-Driven Development — Mandatory

**All implementation MUST follow strict TDD. No exceptions.**

The red-green cycle is non-negotiable:

1. **Write the test first.** Before writing any implementation code, write a test that covers the intended behaviour.
2. **Confirm it fails (red).** Run the test and verify it fails. If it passes without implementation, the test is wrong — fix it before continuing.
3. **Write the implementation (green).** Write only enough code to make the failing test pass.
4. **Verify it passes.** Run the test again and confirm it is green.

> Untested code is faulty code. A task is not complete until its tests exist and pass.
