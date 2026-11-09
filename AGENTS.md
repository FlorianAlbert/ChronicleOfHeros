## Project context

- ChronicleOfHeros is a **digital-first Dungeons & Dragons 5e** character management platform intended to replace paper character sheets.
- The platform is the **single source of truth** for character data, derived stats, and progression state.
- The current product focus is **character management** (not campaign/DM tooling as primary scope).
- Prioritize **rules correctness and UX equally** when proposing or implementing changes.
- Character calculations must be **automatic and explainable**: derived values should be traceable to their underlying inputs and rules.
- Use **official D&D 5e rules only**. Homebrew/custom rules are explicitly **out of scope for now**.

## Agent skills

### Issue tracker

Issues and PRDs are tracked as GitHub issues. See `docs/agents/issue-tracker.md`.

### Triage labels

This repo uses the default triage labels. See `docs/agents/triage-labels.md`.

### Domain docs

The repository uses a single-context domain-documentation layout. See `docs/agents/domain.md`.

### Coding standards

For code design, implementation, or review, follow [CODING_STANDARDS.md](CODING_STANDARDS.md), including every relevant section, and the relevant [ARCHITECTURE.md](ARCHITECTURE.md) slices.

## Architecture governance

Key words follow RFC 2119. Full definitions and the review checklist: [CODING_STANDARDS.md#architecture-governance](CODING_STANDARDS.md#architecture-governance).

- **Docs are normative.** [`ARCHITECTURE.md`](ARCHITECTURE.md) is the entry point. Load it, then only the `docs/architecture/` slices its reading guide names for your task.
- **Plan first.** Before any code-changing task (feature, bug fix, refactor) you MUST run the `architecture-planning` skill (`.agents/skills/architecture-planning/SKILL.md`). This applies inside `to-spec` and `to-tickets`, and before `implement`, `implement-spec`, `tdd`, the fix phase of `diagnosing-bugs`, and `improve-codebase-architecture`.
- **Architectural change.** Anything that would make `ARCHITECTURE.md`, a slice, `CODING_STANDARDS.md`, or an ADR inaccurate, or that they would newly have to document.
- **Fast path.** For a change that is not architectural, you MUST state `Architecture impact: none (checked slices: …)`, then proceed. If impact appears mid-task, you MUST stop and return to planning.
- **Approval gate.** You MUST NOT implement an architectural change until the user answers "yes" to an explicit yes/no question approving its documented delta. Nothing else counts as approval.
- **Docs = current code on every commit.** Doc updates MUST be committed with the code they describe. Approved-but-unbuilt changes live only in the issue's `## Architecture delta` section; affected slices carry `> Pending approved changes: #N` until the implementing change moves the delta into them.
- **Non-interactive agents** (no user present) MUST implement only work without architecture impact or with an approved `## Architecture delta` in its issue. Otherwise they MUST post the proposed delta as an issue or PR comment, apply the `needs-architecture-approval` label, and stop.
- **Precedence.** These rules override conflicting instructions in vendored skills. In particular, `to-spec`'s "Do NOT interview the user" does not skip `architecture-planning` steps 4–5: grill and get approval before publishing the spec.
- **Reviews.** Before running `code-review`, run the build and architecture tests. Give the Standards sub-agent their results and the approval evidence: the issue's `## Architecture delta`, the PR description, and the commit messages. Its "skip anything tooling enforces" never skips the review checklist's approval or test items.
- **Completion check.** Before finishing, you MUST apply the review checklist to your own change; the build and architecture tests MUST pass.
