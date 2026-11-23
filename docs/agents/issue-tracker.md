# Issue tracker: GitHub

Issues and PRDs for this repo live as GitHub issues. Use the `gh` CLI for all operations.

Read a known issue with `gh issue view <number> --comments` first. Only try PR
resolution if its type is unknown; fetch a PR diff only after confirming it is
a PR. This avoids redundant issue/PR requests and irrelevant 404 responses.

## Conventions

- **Create an issue**: `gh issue create --title "..." --body "..."`. Use a heredoc for multi-line bodies.
- **Read an issue**: `gh issue view <number> --comments`, filtering comments by `jq` and also fetching labels.
- **List issues**: `gh issue list --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'` with appropriate `--label` and `--state` filters.
- **Comment on an issue**: `gh issue comment <number> --body "..."`
- **Apply / remove labels**: `gh issue edit <number> --add-label "..."` / `--remove-label "..."`
- **Close**: `gh issue close <number> --comment "..."`

Infer the repo from `git remote -v` — `gh` does this automatically when run inside a clone.

## Pull requests as a triage surface

**PRs as a request surface: no.** _(Set to `yes` if this repo treats external PRs as feature requests; `/triage` reads this flag.)_

When set to `yes`, PRs run through the same labels and states as issues, using the `gh pr` equivalents:

- **Read a PR**: `gh pr view <number> --comments` and `gh pr diff <number>` for the diff.
- **List external PRs for triage**: `gh pr list --state open --json number,title,body,labels,author,authorAssociation,comments` then keep only `authorAssociation` of `CONTRIBUTOR`, `FIRST_TIME_CONTRIBUTOR`, or `NONE` (drop `OWNER`/`MEMBER`/`COLLABORATOR`).
- **Comment / label / close**: `gh pr comment`, `gh pr edit --add-label`/`--remove-label`, `gh pr close`.

GitHub shares one number space across issues and PRs, so a bare `#42` may be either — resolve with `gh pr view 42` and fall back to `gh issue view 42`.

## When a skill says "publish to the issue tracker"

Create a GitHub issue.

## When a skill says "fetch the relevant ticket"

Run `gh issue view <number> --comments`.

## Triage verification and handoff

Report three separate fields before design grilling or agent readiness:

- **Inspection evidence:** the relevant code path.
- **Executed reproduction:** the exact command/scenario and observed result,
  or "not run". Source inspection is not an executed reproduction.
- **Remaining uncertainty:** what has not been demonstrated.

If reproduction is blocked or deferred, explicitly agree that deferral with
the maintainer before proceeding, and carry the limitation into the agent
brief. Missing actionable reproduction details are a strong `needs-info` signal.
Finish with the [Git workflow handoff](git-workflow.md#handoff).

## Architecture design

The [architecture-planning procedure](../../.agents/skills/architecture-planning/SKILL.md)
integrates installed planning skills without modifying them:

- **Grilling/wayfinding:** record decisions, alternatives, evidence, and open
  questions. Planning tickets are not implementation tickets.
- **Spec creation:** publish requirements and settled constraints. For
  architecture-impacting work awaiting design, state
  `Readiness: Awaiting architecture design` and keep `needs-triage`; do not
  apply `ready-for-agent` or `ready-for-human`.
- **Dedicated design:** use the
  [design template](../../.agents/skills/architecture-planning/DESIGN-TEMPLATE.md)
  for `## Architecture design` in the spec issue. It owns the target views,
  assumptions, decisions, proposed ADR/standard wording, and verification.
  If the body limit requires detail comments, index their URLs in that
  section; each detailed view or decision has one authoritative home.
- **Discovery:** first read the task issue, comments, and parent spec. When
  another design may affect the same area, search open issues, for example
  `gh issue list --state open --search '"Architecture design" in:body Keycloak' --json number,title,body`.
  Replace `Keycloak` with relevant area vocabulary; inspect returned designs
  for actual overlap. Search matches are not approval evidence. A failed
  tracker request is a blocker to report, not evidence that no plans exist.
- **Approval:** record `Status: Approved`,
  `Approved by @<user> on <YYYY-MM-DD>`, and the approved scope only after the
  explicit approval question. Unresolved decisions remain Proposed.
  Approval and implementation progress are separate.
- **Ticket creation:** each architectural ticket has an
  `## Architecture contribution` linking the design and specifying its scope,
  prerequisites, required end state, current-doc updates, and verification.
  Review the breakdown. New architectural decisions in intermediate states
  need explicit approval; merely splitting an approved target does not.
  Set native blocking edges only for real prerequisites. Update the parent
  with links to its implementation tickets, overriding an installed skill's
  instruction not to modify the parent.
- **Readiness:** an approved design may make an unsplit spec implementation-ready
  once the rest of triage is complete. A split spec stays the parent
  specification, without an executable readiness label; readiness belongs to
  its reviewed tickets. A blocked ticket can be fully specified, but its open
  blocking edges still prevent starting it.
- **Implementation:** recheck relevant assumptions against living code/docs;
  no baseline commit is required. Report conflicts and reapprove changed
  decisions. Update implementation progress in the issue, not target views
  to pretend the entire design has been built.
- **No human present:** post an unapproved proposal or amendment with
  `gh issue comment <number> --body-file <file>` (or the PR equivalent), apply
  `needs-architecture-approval`, remove executable readiness labels, and stop
  without implementation. After explicit approval, incorporate the proposal
  into the issue's design and remove the waiting label. Finish remaining
  triage or breakdown review before restoring executable readiness.

Current-state slices carry no pending-design pointers. Existing approved
legacy deltas remain valid for their scope; migrate draft material only with
permission and verify its preservation before removing it from current docs.

## Wayfinding operations

Used by `/wayfinder`. The **map** is a single issue with **child** issues as tickets.

- **Map**: a single issue labelled `wayfinder:map`, holding the Notes / Decisions-so-far / Fog body. `gh issue create --label wayfinder:map`.
- **Child ticket**: an issue linked to the map as a GitHub sub-issue (`gh api` on the sub-issues endpoint). Where sub-issues aren't enabled, add the child to a task list in the map body and put `Part of #<map>` at the top of the child body. Labels: `wayfinder:<type>` (`research`/`prototype`/`grilling`/`task`). Once claimed, the ticket is assigned to the driving dev.
- **Blocking**: GitHub's **native issue dependencies** — the canonical, UI-visible representation. Add an edge with `gh api --method POST repos/<owner>/<repo>/issues/<child>/dependencies/blocked_by -F issue_id=<blocker-db-id>`, where `<blocker-db-id>` is the blocker's numeric **database id** (`gh api repos/<owner>/<repo>/issues/<n> --jq .id`, _not_ the `#number` or `node_id`). GitHub reports `issue_dependencies_summary.blocked_by` (open blockers only — the live gate). Where dependencies aren't available, fall back to a `Blocked by: #<n>, #<n>` line at the top of the child body. A ticket is unblocked when every blocker is closed.
- **Frontier query**: list the map's open children (`gh issue list --state open`, scoped to the map's sub-issues / task list), drop any with an open blocker (`issue_dependencies_summary.blocked_by > 0`, or an open issue in the `Blocked by` line) or an assignee; first in map order wins.
- **Claim**: `gh issue edit <n> --add-assignee @me` — the session's first write.
- **Resolve**: `gh issue comment <n> --body "<answer>"`, then `gh issue close <n>`, then append a context pointer (gist + link) to the map's Decisions-so-far.
