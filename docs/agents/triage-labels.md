# Triage Labels

| Label in mattpocock/skills | Label in our tracker | Meaning |
| --- | --- | --- |
| `needs-triage` | `needs-triage` | Maintainer needs to evaluate this issue |
| `needs-info` | `needs-info` | Waiting on reporter for more information |
| `ready-for-agent` | `ready-for-agent` | Fully specified, ready for an AFK agent |
| `ready-for-human` | `ready-for-human` | Requires human implementation |
| `wontfix` | `wontfix` | Will not be actioned |
| — (repo-specific) | `needs-architecture-approval` | Waiting for explicit approval of a proposed architecture design or amendment; not implementation-ready |

When a skill mentions a role, use the corresponding label string from this table.
Repo-specific rows have no skill role. The
[architecture-design readiness rules](issue-tracker.md#architecture-design)
override installed skills' automatic ready labels: publishing a spec awaiting
design does not make it executable, and a split spec delegates executable
readiness to its tickets.
