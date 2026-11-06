---
name: architecture-planning
description: "Mandatory architecture gate for every code change: classify architecture impact, then draft, grill, and get explicit approval for the architecture delta before implementing. Use before any feature, bug fix, or refactor; while running `to-spec` or `to-tickets`; before `implement`, `implement-spec`, `tdd`, `improve-codebase-architecture`, or the fix phase of `diagnosing-bugs`; and whenever the user wants to plan, discuss, or change architecture."
---

# Architecture planning

Architecture docs are normative and describe the current code on every commit. An architectural change ships only after the user explicitly approves its **delta**: the before/after edit it causes to `ARCHITECTURE.md`, the slices under `docs/architecture/`, ADRs, or `CODING_STANDARDS.md`. What counts as architectural is defined once, in [`CODING_STANDARDS.md#architecture-governance`](../../../CODING_STANDARDS.md#architecture-governance).

Every code-changing task runs steps 1–2. Steps 3–7 run only when step 2 finds impact. When no user can answer, the [non-interactive branch](#non-interactive-branch) replaces steps 4–6.

## 1. Load the relevant slices

1. You MUST read `ARCHITECTURE.md` and follow its reading guide for this task (fall back to the slice index's `Load when:` lines). Load only the slices it routes to; add `docs/architecture/conventions.md` when you will draft diagrams.
2. Read the ADRs in `docs/adr/` that touch the area and the `GLOSSARY.md` terms you will use.
3. If the task has an issue, read it (`gh issue view <n> --comments`) and look for an `## Architecture delta` section and its status line.
4. Note any `> Pending approved changes: #N` pointer or `## Known deviations` section in a loaded slice: that slice is not the whole story yet.

Done when you can name every slice the reading guide routes this task to and whether an approved delta exists.

## 2. Classify impact

Check the planned change, including code you expect to touch on the way, against every item of the architectural-change definition. Take exactly one outcome:

- **None** (the **fast path**). Output exactly `Architecture impact: none (checked slices: <slice paths>)` and hand back to the calling workflow.
- **Covered by an approved delta.** The issue's `## Architecture delta` carries an `Approved by @<user> on <YYYY-MM-DD>` line and its Before views still match the slices. Implement it faithfully through step 6a without re-approval: apply its After views to the slices and remove the `> Pending approved changes: #N` pointers. A deviation forced by reality, or Before views that no longer match, goes through step 3 as a new delta.
- **Impact.** Continue with step 3.

**Bug fixes** mostly take the fast path. A fix that adds, removes, or reroutes a documented collaboration (a call, dependency, sequence step, or state transition) has impact.
**Refactors** that change documented structure (moving a type across layers or projects, renaming or splitting a documented interface or class) are architectural even when behaviour is unchanged.

**Mid-task trigger.** When impact surfaces after you took the fast path, you MUST stop coding and enter step 3; resume only once the delta is approved.

Done when you have printed the fast-path line, named the approved delta you will implement, or listed every affected slice, ADR, and standard.

## 3. Draft the delta in the slices

1. Edit each affected slice in the working tree so it shows the target state. Mark elements with the `added`, `removed`, and `changed` classDefs from `docs/architecture/conventions.md`; `removed` elements stay drawn until implementation. Verified views (`%% verify:` / `<!-- verify: -->`) MUST stay within the parseability rules of conventions.md.
2. Draft the wording changes the delta forces in ADRs and `CODING_STANDARDS.md`. Propose a new ADR only for a hard-to-reverse trade-off, judged by the `domain-modeling` ADR criteria.
3. In chat, summarise the delta, its rationale, and at least one alternative considered with why it lost; list every changed file; point the user at the rendered slices (Markdown preview) and `git --no-pager diff -- ARCHITECTURE.md docs CODING_STANDARDS.md`.

Done when every affected view shows the target state with highlights and the chat summary names every changed file.

## 4. Grill open decisions

Invoke the `grilling` skill on the decisions the draft leaves open. Fold each round's answers into the slices and point the user at the updated diff.

Done when the grilling frontier is empty and the user confirms shared understanding.

## 5. Get explicit approval

Ask one yes/no question with your user-question tool (e.g. `ask_user`): `Approve this architecture delta? <one-line summary>; slices: <paths>`. Only an explicit yes to that question approves the delta. Silence, "continue", a yes to any other question, or a yes to an earlier draft leaves it unapproved: ask again. A no returns you to step 4.

Record `Approved by @<user> on <YYYY-MM-DD>` with the summary that was approved (`gh api user --jq .login` when you lack the handle). Any later edit to the delta MUST be approved again.

Done when you hold an approval line for exactly the delta in the working tree.

## 6. Hand off the approved delta

### 6a. Implement now (same session)

1. Implement exactly the approved delta. When the code needs to deviate from it, stop and return to step 3 with the deviation drafted.
2. Strip the highlight styling (the `classDef added|removed|changed` lines and their `class`/`cssClass`/`:::` assignments) and delete `removed` elements, so the slices describe the resulting code.
3. Commit docs and code together in one commit, with the approval line in the commit message or PR body.

### 6b. Spec or ticket flow (implemented later)

1. Write the approved Before/After, approval line filled, into the `## Architecture delta` section of the spec issue (or of the ticket that implements the change when `to-tickets` splits the work), using [DELTA-TEMPLATE.md](DELTA-TEMPLATE.md).
2. Revert the slice drafts (`git restore <slices>`), add `> Pending approved changes: #N` near the top of each affected slice (placed as conventions.md shows), and commit the pointers as a docs-only change.
3. The implementing PR later enters at step 2 ("covered by an approved delta"): it moves the After views into the slices without highlights and removes the pointers.

## 7. Completion check

Done when every box holds:

- [ ] Every affected slice matches the code, and no draft styling remains (`git grep -nE "classDef (added|removed|changed)" -- ARCHITECTURE.md docs` finds nothing).
- [ ] `> Pending approved changes: #N` pointers are added (6b) or removed (when implementing #N); `## Known deviations` reflects reality.
- [ ] Each touched ADR has an accurate status line under its H1 (e.g. `Status: Accepted, implementation pending (#N)`) and consistent content; any new ADR follows `domain-modeling` conventions.
- [ ] `CODING_STANDARDS.md` is updated wherever a rule changed, and its architecture-governance review checklist passes for this change.
- [ ] Every verified view (`%% verify:` / `<!-- verify: -->`) affected by the change is updated.
- [ ] `dotnet build` and `dotnet test tests/ChronicleOfHeros.Architecture.Tests` pass; the doc-conformance tests compare verified views with the code.

## Non-interactive branch

With no user present (Copilot coding agent, background agent, CI), you implement only fast-path work or work covered by an approved delta. For any other impact your deliverable is the proposal, and you MUST NOT implement any part of the unapproved change:

1. Run steps 1–3, then write the delta in the [DELTA-TEMPLATE.md](DELTA-TEMPLATE.md) format with `Status: Proposed — awaiting approval`.
2. Post it as a comment on the issue (`gh issue comment <n> --body-file -`, body on stdin) or PR (`gh pr comment <n> --body-file -`).
3. Apply the label: `gh issue edit <n> --add-label needs-architecture-approval` (`gh pr edit` for a PR).
4. Revert the slice drafts and stop; report the comment URL.

Done when the comment is posted, the label is applied, and the working tree holds no change from this task.
