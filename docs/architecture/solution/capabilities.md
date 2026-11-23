# Solution: capabilities

Scope: which capabilities exist, what each owns, and how hosts install and consume them.
Load when: adding a capability, moving behavior between capabilities or hosts, or wiring a capability into a host.
Parent: [ARCHITECTURE.md](../../../ARCHITECTURE.md) · Neighbours: [projects.md](projects.md)

No product capability is currently implemented as a contracts-and-implementation project pair. The API currently exposes no application operations. Introduce a capability when product behavior is added, following [ADR 0007](../../adr/0007-contract-first-capability-architecture.md).
