# Git workflow

## Setup

Run `pwsh -File scripts\Install-GitHooks.ps1` from the repository root after
cloning. The installer configures this clone's `core.hooksPath` and refuses to
replace a different existing hooks configuration. The repository pre-commit
hook rejects commits on `main` and detached HEAD.

The hook is a local guardrail, not server-side branch protection; Git can bypass
hooks. CI tests the hook behavior in an isolated temporary repository with
`pwsh -File scripts\Test-GitHooks.ps1`. On Unix, use `/` in script paths.

## Task branch and permissions

Before persistent repository edits, inspect `git status --short` and
`git branch --show-current`. Use a task branch. Continue an existing branch only
when it belongs to the task; create unrelated work from the intended base
instead of stacking it on another task. Preserve unrelated worktree changes.

Architecture approval approves the design, not Git operations. Obtain explicit
permission before committing, pushing, or creating a PR; identify the branch
and intended operation. Recheck the branch immediately before a commit.
Stage only task-owned files. When a workflow requires a commit but permission
is absent, leave the validated changes uncommitted and report that handoff.

## Handoff

Finish triage or implementation by naming:

- the current branch and related issue;
- committed versus uncommitted changes, and whether anything was pushed;
- validation performed and remaining uncertainty;
- the next action: continue implementation on this branch, or open a PR
  against the intended base once implementation and validation are complete.

Approved architecture deltas live in the issue. Pending pointers may accompany
the implementation on its task branch; they are not a reason to commit on
`main`. Implementing the delta updates the slices and removes its pointers.
