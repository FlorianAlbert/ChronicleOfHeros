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

For code design, implementation, or review, follow [CODING_STANDARDS.md](CODING_STANDARDS.md), including every relevant section.
