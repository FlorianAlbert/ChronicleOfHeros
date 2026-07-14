# Copilot Instructions — ChronicleOfHeros

## Project Context — Always Assume

- ChronicleOfHeros is a **digital-first Dungeons & Dragons 5e** character management platform intended to replace paper character sheets.
- The platform is the **single source of truth** for character data, derived stats, and progression state.
- The current product focus is **character management** (not campaign/DM tooling as primary scope).
- Prioritize **rules correctness and UX equally** when proposing or implementing changes.
- Character calculations must be **automatic and explainable**: derived values should be traceable to their underlying inputs and rules.
- Use **official D&D 5e rules only**. Homebrew/custom rules are explicitly **out of scope for now**.

## Test-Driven Development — Mandatory

**All implementation MUST follow strict TDD. No exceptions.**

The red-green cycle is non-negotiable:

1. **Write the test first.** Before writing any implementation code, write a test that covers the intended behaviour.
2. **Confirm it fails (red).** Run the test and verify it fails. If it passes without implementation, the test is wrong — fix it before continuing.
3. **Write the implementation (green).** Write only enough code to make the failing test pass.
4. **Verify it passes.** Run the test again and confirm it is green.

> Untested code is faulty code. A task is not complete until its tests exist and pass.
