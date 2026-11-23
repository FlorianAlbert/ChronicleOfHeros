## Project context

- ChronicleOfHeros is a **digital-first Dungeons & Dragons 5e** character management platform intended to replace paper character sheets.
- The platform is the **single source of truth** for character data, derived stats, and progression state.
- The current product focus is **character management** (not campaign/DM tooling as primary scope).
- Prioritize **rules correctness and UX equally** when proposing or implementing changes.
- Character calculations must be **automatic and explainable**: derived values should be traceable to their underlying inputs and rules.
- Use **official D&D 5e rules only**. Homebrew/custom rules are explicitly **out of scope for now**.

## Agent skills

### Issue tracker

For issue/PR operations and triage, follow [the issue tracker guide](docs/agents/issue-tracker.md), including verification evidence and handoff.

### Triage labels

This repo uses the default triage labels. See `docs/agents/triage-labels.md`.

### Domain docs

The repository uses a single-context domain-documentation layout. See `docs/agents/domain.md`.

### Third-party skills

Installed third-party skills are updated only from upstream. Keep repository
integration in repository-owned docs and pointers, not in those skills.

### .NET API lookup

For repository API questions, read the dependency manifest first and scope
`dotnet-inspect` to the exact package ID/version. Search member names with
`--members` and reuse the returned type/scope; expand to a package family only
when the exact scope fails. A type-search miss does not establish member absence.

### Coding standards

For code design, implementation, or review, follow [CODING_STANDARDS.md](CODING_STANDARDS.md), including every relevant section, and the relevant [ARCHITECTURE.md](ARCHITECTURE.md) slices.

## Workflow gates

- **Git:** Before repository edits or Git writes, follow [the Git workflow](docs/agents/git-workflow.md), including hook installation, task branches, operation permissions, and handoff.
- **Planning:** Run [architecture-planning](.agents/skills/architecture-planning/SKILL.md) at the current stage: explore during grilling/wayfinding; synthesize during `to-spec`; perform dedicated architecture design after the spec and before `to-tickets`; scope contributions during ticket creation. Carry settled decisions forward. Publishing an architecture-impacting spec does not mark it implementation-ready or automatically start the next stage.
- **Architecture:** Before every code change, run the skill's impact/approval check. Load [ARCHITECTURE.md](ARCHITECTURE.md) and its selected slices. The skill and [architecture governance](CODING_STANDARDS.md#architecture-governance) own approval and completion checks.
- **Precedence:** Repository governance overrides conflicting installed-skill instructions, including automatic readiness labels and inline ADR writes. Proposed architecture and ADR/standard wording stay in the issue; current-state docs change with implementation.
- **Reviews:** Before `code-review`, run the build and architecture tests. Give the Standards reviewer the results and approval evidence from the design, ticket contribution, PR description, and commit messages. Tool-enforced checks do not replace the governance approval/test checklist.
