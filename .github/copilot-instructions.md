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
Never implement a feature outside of an active OpenSpec change. The available skills (`openspec-new-change`, `openspec-continue-change`, `openspec-apply-change`, `openspec-verify-change`, `openspec-archive-change`, `openspec-sync-specs`, `openspec-explore`) contain the full operational details — invoke them instead of improvising.

## Requirement Discussions — Always Grill

Whenever a requirement is being discussed or developed, invoke the `grilling` skill to stress-test the specific OpenSpec artifact currently being created or updated.

**Critical sequencing rule:** Before invoking `openspec-explore`, `openspec-new-change`, or `openspec-continue-change`, the agent MUST invoke `grilling` for that artifact scope first.
For the **spec-driven schema**, grill each artifact on the following scope:
1. **proposal.md**: Grill the **what/why** — problem statement, goals, scope boundaries, capabilities, and impact.
2. **specs/<capability>/spec.md**: Grill requirement quality — expected behaviors, acceptance criteria, rules correctness, and edge cases.
3. **design.md**: Grill the **how** — architecture, key technical decisions, alternatives/tradeoffs, and risks/mitigations.
4. **tasks.md**: Grill execution readiness — task completeness, ordering/dependencies, TDD coverage plan, and done criteria.
For other schemas, apply the same artifact-scoped principle using the schema's artifact instructions.
Do not invoke `grilling` again for the same artifact scope unless materially new scope, constraints, or decisions have been introduced.

## Test-Driven Development — Mandatory

**All implementation MUST follow strict TDD. No exceptions.**

The red-green cycle is non-negotiable:

1. **Write the test first.** Before writing any implementation code, write a test that covers the intended behaviour.
2. **Confirm it fails (red).** Run the test and verify it fails. If it passes without implementation, the test is wrong — fix it before continuing.
3. **Write the implementation (green).** Write only enough code to make the failing test pass.
4. **Verify it passes.** Run the test again and confirm it is green.

> Untested code is faulty code. A task is not complete until its tests exist and pass.
