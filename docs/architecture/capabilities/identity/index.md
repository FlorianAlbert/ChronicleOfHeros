# Identity sub-index

Scope: routes to the Identity capability's slices.
Load when: a task touches accounts, roles, sign-in, tokens, refresh sessions, Player administration, or the Identity database.
Parent: [ARCHITECTURE.md](../../../../ARCHITECTURE.md) · Neighbours: [../../solution/capabilities.md](../../solution/capabilities.md)

The Identity capability is `ChronicleOfHeros.Identity.Contracts` plus `ChronicleOfHeros.Identity.AspNetCore`. It owns Player accounts, roles, passwords, refresh sessions, JWT issuing and validation, and authorization policies.

| Slice | Load when |
|---|---|
| [components.md](components.md) | Moving a type between layers or namespaces; adding a dependency between layers or on the contracts. |
| [classes.md](classes.md) | Changing a contract interface, service, token handling, registration, or hosted service. |
| [domain.md](domain.md) | Changing roles, Username rules, temporary credentials, password rules, or Player and Operator concepts. |
| [persistence.md](persistence.md) | Changing entities, the `DbContext`, configurations, or migrations. |
| [state.md](state.md) | Changing the must-change-password rule or the refresh-session lifecycle. |

Related: [../../flows/authentication.md](../../flows/authentication.md), [../../flows/player-administration.md](../../flows/player-administration.md), [../../cross-cutting/security.md](../../cross-cutting/security.md).
