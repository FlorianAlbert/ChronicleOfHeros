---
name: architecture-planning
description: "Stage-aware architecture planning: explore constraints during grilling/wayfinding, synthesize specs, design explicitly between spec and tickets, scope ticket transitions, and check approval before implementation. Use during planning and before any code change, including implement, implement-spec, tdd, refactoring, and bug fixes."
---

# Architecture planning

Current-state docs describe implemented reality. Target architecture lives in
the spec issue's `## Architecture design`, separate from current-state views.
[Architecture governance](../../../CODING_STANDARDS.md#architecture-governance)
defines impact and approval; this repository-owned skill owns the procedure.
Installed skills stay upstream-owned; integrate them through
[AGENTS.md](../../../AGENTS.md#workflow-gates) and this procedure.

## 1. Orient and classify

1. Read `ARCHITECTURE.md` and the slices its reading guide selects, applicable
   ADRs, standards, and glossary terms. Read the task issue and comments and its
   parent spec when present. An ADR under revision still governs current code.
2. Discover related open designs through the
   [issue tracker guide](../../../docs/agents/issue-tracker.md#architecture-design).
   Inspect relevant decisions and conflicts, not every unrelated proposal.
3. Check all expected changes against the architectural-change definition.
   For no impact, state exactly:
   `Architecture impact: none (checked slices: <slice paths>)`.
   Hand back to the calling workflow.
4. Otherwise name the affected views, ADRs, and standards, then take the
   matching stage below. Planning is not implementation permission.

Done when impact, relevant existing decisions, and the next stage are explicit.
If impact emerges while coding, stop and enter the design stage.

## 2. Take the current planning stage

### Explore: grilling and wayfinding

Investigate architecture where it affects requirements, feasibility, scope,
or trade-offs. Use `grilling` for open decisions; research or prototype only
when evidence is needed. Carry settled decisions and their rationale forward.
Wayfinding retains its own map and decision-ticket lifecycle.

Record candidate decisions, evidence, and unresolved questions in the
conversation or tracker. Proposed ADR or standard wording belongs with the
proposal, not in current-state documents. This also applies when
`domain-modeling` or `grill-with-docs` would otherwise write an ADR inline.

Done when the calling exploration stage's questions are resolved or recorded
as open. Hand back without demanding a complete architecture or approval.

### Specify: to-spec

Synthesize requirements, scope, acceptance criteria, known constraints, and
settled decisions. List remaining architecture questions and link their
evidence. Preserve technical choices already made without inventing answers
to unresolved ones or restarting their interviews.

For architectural work, publish the spec as awaiting architecture design,
without `ready-for-agent` or `ready-for-human`. Its next stage is the dedicated
design step, not implementation or automatic ticket creation. If no
architectural decisions remain because a design is already approved, link it
and preserve that status.

Done when the spec states its readiness and the next stage. Hand back; spec
publication alone does not run or approve the design stage.

### Design: after the spec, before tickets

1. Follow the [Git workflow](../../../docs/agents/git-workflow.md) before any
   repository edit. Draft in the conversation or issue using
   [DESIGN-TEMPLATE.md](DESIGN-TEMPLATE.md). Keep current-state slices intact.
2. Reference relevant current docs and state the assumptions the design needs.
   Inspect living code and docs; a pinned baseline commit is not required.
3. Draw clean target views at the levels required by architecture conventions.
   Each view depicts only the target. Explain removals and replacements in a
   separate change summary. Include proposed ADR/standard wording and tests.
   Issue-size limits may require linked detail comments on the same spec;
   index them in the design section and give each detail one authoritative home.
4. Reuse settled decisions. Invoke `grilling` only for remaining architectural
   decisions or contradictions. When a product trade-off changes the spec,
   return that specific question to discussion and update the spec explicitly.
5. Once open architectural decisions are resolved, summarize the target,
   rationale, alternatives, affected documents, and validation requirements.
   Confirm shared understanding, then ask with `ask_user`:
   `Approve this architecture design? <summary>; affected docs: <paths>`.
   Only an explicit affirmative answer to that approval question counts.
6. Record `Approved by @<user> on <YYYY-MM-DD>` and the approved scope in the
   issue. Changed architectural decisions return the design to Proposed and
   require renewed approval; editorial changes do not.

Done when the spec holds an approved, implementable target or explicitly lists
what still blocks approval. An unapproved design is not implementation-ready.

### Decompose: to-tickets

1. Read the spec and approved design. If required architectural decisions are
   unresolved, return to design rather than publishing ready tickets.
2. Follow the calling skill's slicing and breakdown review. Each architectural
   ticket links the design and states its contribution, prerequisites, required
   end state, current-doc updates, and verification. Use
   `## Architecture contribution` after its acceptance criteria.
3. Draw a clean intermediate-state view only when prose cannot unambiguously
   specify a safe transition. Every commit must keep code and current docs
   consistent, including work on a shared integration branch.
4. Dependencies express actual prerequisites. Editing the same slice alone does
   not make two tickets dependent. Review the route with the user; seek new
   architecture approval only for changed target decisions or newly introduced
   intermediate-state decisions. Record that approval alongside the transition.
5. Publish only the approved breakdown, with readiness matching the tracker
   guide. The parent spec is the specification, not a second executable ticket.

Done when every ticket has an unambiguous scope and end state, every real
prerequisite is recorded, and any new architectural decisions are approved.

### Implement: approved work or a small direct change

1. Read the approved design and the ticket's contribution and prerequisites.
   Recheck relevant assumptions against current code and docs before starting.
   Unrelated drift does not invalidate approval. On a conflict, stop and
   reconcile it; changed architectural decisions require renewed approval.
2. A small direct task needs neither a spec issue nor a ticket breakdown.
   Use a compact conversation proposal with the same current/target separation,
   decisions, verification, and explicit approval. Preserve its approval
   evidence in the handoff and, when authorized, the commit or PR.
3. Implement only the approved scope. Update affected current-state views,
   verified views, ADRs, and standards with the code they describe. An
   intermediate ticket documents its resulting state, not the final target.
4. Apply the governance review checklist. Run the build and architecture tests:

   ```text
   dotnet build --artifacts-path artifacts
   dotnet test --project tests\ChronicleOfHeros.Architecture.Tests\ChronicleOfHeros.Architecture.Tests.csproj --artifacts-path artifacts
   ```

5. Update implementation progress in the tracker without replacing the target
   design with a partially built state. Follow the Git workflow for permissions
   and handoff; approval does not authorize commits, pushes, or PR creation.

Done when the approved contribution is implemented and verified, current docs
match the code, tracker progress is accurate, and approval evidence is retained.

## Non-interactive work

Implement only no-impact work or a contribution covered by explicit approval.
For unapproved impact, post a Proposed design or a scoped amendment as an
issue/PR comment, apply `needs-architecture-approval`, and stop without
implementing it. Use the template; leave unresolved decisions visible.
If posting is unavailable, report the blocker and preserve the proposal in
the handoff rather than claiming it was published.

## Existing plans

An approved legacy `## Architecture delta` remains valid for its documented
scope. Revalidate relevant assumptions; do not require mechanical conversion
or rebuild its Before/After blocks in the slices. New or revised decisions use
the design format. Migrate existing draft material only with permission:
capture and verify its preservation in the issue before removing it from
current docs. Retain decision/evidence history and its unapproved status.
