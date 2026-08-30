# Architecture Work

Use this workflow when adding or refactoring a capability, changing composition or dependency injection, adding EF Core persistence or migrations, writing a database-backed test, or declaring a C# extension.

1. Read `docs/contract-first-capability-architecture.md` and every relevant ADR in `docs/adr/` before editing.
	For C# changes, also follow `.github/instructions/csharp-architecture.instructions.md`.
2. Keep the public seam narrow: capability consumers use contracts; executable hosts and capability tests enter implementations only through the registration API.
3. For relational behavior, test through public contracts and capability registration against PostgreSQL in Testcontainers. Apply migrations through the capability's migration registration option before exercising runtime behavior.
4. Before completion, inspect every changed registration, context, contract, and extension declaration against the architecture guide; run the narrow test and build that can disprove the change.

Completion: the relevant architecture guide rules hold for every changed capability boundary, composition path, persistence model, database test, and C# extension declaration.