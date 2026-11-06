# Triage Labels

| Label in mattpocock/skills | Label in our tracker | Meaning |
| --- | --- | --- |
| `needs-triage` | `needs-triage` | Maintainer needs to evaluate this issue |
| `needs-info` | `needs-info` | Waiting on reporter for more information |
| `ready-for-agent` | `ready-for-agent` | Fully specified, ready for an AFK agent |
| `ready-for-human` | `ready-for-human` | Requires human implementation |
| `wontfix` | `wontfix` | Will not be actioned |
| — (repo-specific) | `needs-architecture-approval` | Waiting for the user to approve a proposed architecture delta; agents do not implement it until approved |

When a skill mentions a role, use the corresponding label string from this table. Edit the right-hand column to match the tracker vocabulary. Repo-specific rows have no skill role; `needs-architecture-approval` follows `## Architecture delta` in `docs/agents/issue-tracker.md`.
