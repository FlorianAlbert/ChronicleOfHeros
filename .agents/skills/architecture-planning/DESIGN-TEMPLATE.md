# Architecture design template

Use in the spec issue after `## Implementation Decisions`, or for a compact
direct-task proposal. Current-state docs stay untouched while planning.

## Filling rules

- Start Proposed. Record approval only after the explicit approval question.
  Architectural decision changes require renewed approval; editorial edits do
  not. State open decisions rather than presenting candidates as settled.
- Reference living current-state slices and the assumptions that matter.
  Recheck them when implementation starts; no baseline commit is required.
- Target diagrams and inventories depict only the intended state. Put
  removals/replacements in the separate change summary, not in target views.
  Use the architecture conventions' levels and vocabulary without draft
  highlights. Proposed verified views are not claims of existing conformance.
- Give each affected view enough detail for implementation. When the tracker
  body limit requires detail comments, link them from this section and keep
  each detail in one authoritative place. Update the index when revising it.
- Include exact proposed ADR/standard wording when it changes a decision or
  rule. It becomes governing documentation with the implementation, not before.
- Issue links to repository docs must be absolute GitHub URLs; relative links
  do not resolve there.

## Spec design

````markdown
## Architecture design

Status: Proposed - awaiting decisions and approval
Readiness: Not ready for implementation

### Intent and scope

<What this design enables; link the spec requirements it satisfies.>

### Current-state references and assumptions

- [<current slice>](https://github.com/FlorianAlbert/ChronicleOfHeros/blob/main/docs/architecture/<path>.md): <relevant fact/assumption to recheck>

### Target architecture

#### <Affected area and view>

```mermaid
<Clean target state, containing only elements that will exist.>
```

<Responsibilities, contracts, rules, and failure semantics not shown above.>

### Change summary

- <What is introduced, removed, or replaced relative to current reality.>

### Decisions and alternatives

- **<Decision>**: <why>. Alternative: <option and why it lost>.

### Open questions

- <Unresolved decision or contradiction; None when ready for approval.>

### ADRs and standards

- <Proposed decision/rule wording and status changes, or None.>

### Verification

- <Behavioral acceptance tests, relevant verified views, architecture rules,
  and required integration evidence. Distinguish existing evidence from work
  still required.>
````

After approval, use `Status: Approved` and
`Approved by @<user> on <YYYY-MM-DD>`, followed by the approved scope.
Implementation progress is separate from target-design approval.

## Ticket contribution

```markdown
## Architecture contribution

Design: <spec issue link, including any relevant detail comment>
Contribution: <the part of the approved target this ticket implements>
Prerequisites: <required existing state and blocking tickets, or None>
Required end state: <observable architecture after this ticket, not the entire target>
Current-doc updates: <affected views, ADRs, standards>
Verification: <tests/evidence required for this increment>
```

Add a clean intermediate-state view when necessary for an unambiguous
transition. New architectural decisions in that transition require their own
explicit approval evidence; referencing the target does not approve them.
