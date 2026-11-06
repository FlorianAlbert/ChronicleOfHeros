# Architecture documentation conventions

Scope: slice structure, diagram rules, draft highlighting, and the verified-view specification.
Load when: writing, drafting, or reviewing a slice or an `## Architecture delta`; implementing or fixing doc-conformance tests.
Parent: [ARCHITECTURE.md](../../ARCHITECTURE.md)

## Slice structure

- One slice shows one view of one area, at `docs/architecture/<area>/<view>.md`. An area gets an `index.md` sub-index only when it has more than three slices.
- A slice starts with this header block:

  ```text
  # <Area>: <view>

  Scope: <one line>.
  Load when: <one line naming the tasks that need this slice>.
  Parent: [<ARCHITECTURE.md or index.md>](<relative link>) · Neighbours: [<slice>](<relative link>), …
  ```

- Links are relative. Split a slice when it passes ~200 lines or mixes concerns.
- A slice describes the current code, using [GLOSSARY.md](../../GLOSSARY.md) terms. Approved but unbuilt changes never appear in a slice.
- A slice whose code departs from an ADR or `CODING_STANDARDS.md` ends with `## Known deviations`, one bullet per departure:

  ```text
  - <What the code does>. Deviates from [<ADR or standard>](<link>): <what it requires>. Tracking: <#N or "not yet filed">.
  ```

## Pending approved changes

An approved but unbuilt change lives in its issue's `## Architecture delta`. Each slice the delta changes carries one pointer line directly after the header block, before any other content. It names every issue whose `## Architecture delta` holds part of the change, comma-separated:

```text
> Pending approved changes: #76, #77
```

The implementing change moves the delta into the slice and removes its issue number; it removes the line once no number is left.

## Diagrams

Diagrams are Mermaid fenced blocks (info string `mermaid`). Inventories are Markdown tables.

| Level | Content | Diagram |
|---|---|---|
| 1 | System context | `flowchart` |
| 2 | Containers and deployment (Aspire resources) | `flowchart`, verified tables |
| 3 | Capabilities, projects, project references, internal layers | `flowchart` |
| 4 | Runtime flows | `sequenceDiagram` |
| 5 | Domain model per capability | `classDiagram` |
| 6 | Persistence model per `DbContext` | `erDiagram`, verified table |
| 7 | State machines that exist in code | `stateDiagram-v2` |
| 8 | Class level where helpful | `classDiagram` |

- **Sequence diagrams** are required for every non-trivial flow: one that crosses two or more hosts or involves three or more collaborators.
- **Class diagrams** show the types that explain collaboration: contract interfaces, application services and ports, the infrastructure adapters implementing them, entities, aggregates and value objects, rule and calculation types, endpoint adapters, and hosted services. Show relationships and the public operations of interfaces and ports. Leave out private members, DTO property lists, trivial helpers, and generated code such as migrations.
- Class-diagram notation:
  - Stereotypes: `<<interface>>`, `<<service>>`, `<<entity>>`, `<<rule>>`, `<<endpoint>>`, `<<hosted>>`, `<<options>>`, `<<registration>>`.
  - Operations: `+Name(ParameterType, …) ResultType`. Omit `Task<…>` and `CancellationToken`; write generics with `~`, e.g. `IdentityOperationResult~TokenPairResponse~`.
  - Relationships: `A ..|> B` A implements B; `A <|-- B` B inherits A; `A ..> B : label` A uses B; `A --> B : label` A holds or navigates to B.
- Node ids are simple identifiers (`[A-Za-z][A-Za-z0-9_]*`) with the full name in `["label"]`.

## Draft highlighting

Used only while drafting a delta (see `.agents/skills/architecture-planning/SKILL.md`); never committed in a slice.

| Class | Style | Meaning |
|---|---|---|
| `added` | `fill:#dafbe1,stroke:#1a7f37` | The delta adds the element. |
| `removed` | `fill:#ffebe9,stroke:#cf222e,stroke-dasharray:5 5` | The delta removes the element; it stays drawn until implementation. |
| `changed` | `fill:#fff8c5,stroke:#9a6700` | The element's relationships, operations, or attributes change. |

- `flowchart`, `classDiagram`, `stateDiagram-v2`, `erDiagram`: declare each class used, at the end of the block, as `classDef <class> <style>`. Mark elements with `<id>:::<class>` or `class <id>,<id> <class>` (class diagrams also accept `cssClass "<id>" <class>`).
- `sequenceDiagram`: wrap affected messages in `rect rgb(218,251,225)` (added), `rect rgb(255,235,233)` (removed), or `rect rgb(255,248,197)` (changed); the first line inside the `rect` is `%% draft: <class>`.
- Tables: prefix the first cell of an affected row with `(added) `, `(removed) `, or `(changed) `.
- Before committing, this command MUST print nothing:

  ```text
  git grep -nE "classDef (added|removed|changed)|%% draft:|^\| \((added|removed|changed)\) " -- ARCHITECTURE.md docs/architecture ":!docs/architecture/conventions.md"
  ```

## Verified views

A verified view is a diagram or table that doc-conformance tests in `tests/ChronicleOfHeros.Architecture.Tests` compare with the code in both directions: everything drawn exists in the code, and everything in scope in the code is drawn.

### Markers

- Scanned files: `ARCHITECTURE.md` and every `*.md` below `docs/architecture/`. Files are UTF-8; lines end in LF or CRLF, and parsers strip a trailing CR before matching.
- Fences follow CommonMark: an opening fence is three or more backticks plus an optional info string; a closing fence has at least as many backticks and nothing else.
- **Mermaid view:** a fence whose info string is exactly `mermaid` and whose first content line, trimmed, matches `^%% verify: (?<id>[a-z0-9-]+(:[A-Za-z0-9-]+)?)$`.
- **Table view:** outside fences, a line that, trimmed, matches `^<!-- verify: (?<id>[a-z0-9-]+(:[A-Za-z0-9-]+)?) -->$`. The next line is the table's header row.
- Each id appears exactly once across the scanned files. A duplicate id, an unknown id, or a missing required id fails.
- Examples of markers in documentation stay inside `text` fences, as here, so they are never scanned.

### Mermaid grammar

Every verified Mermaid view is a flowchart. After the marker, each line, trimmed, is one of:

| Line | Form |
|---|---|
| Header, exactly once, before nodes and edges | `flowchart <dir>` or `graph <dir>`; `<dir>` is `TB`, `TD`, `BT`, `LR`, or `RL` |
| Node | `<id>["<label>"]`, optionally followed by `:::<class>` (drafts only) |
| Edge | `<id> --> <id>` |
| Comment | starts with `%%` |
| Draft styling, ignored | `classDef <class> <style>` or `class <id>[,<id>…] <class>` |
| Blank | empty |

- `<id>` matches `[A-Za-z][A-Za-z0-9_]*`; `<label>` has one or more characters, none of them `"`.
- Ids and labels are unique within the view. Every edge endpoint is a declared node. Self-loops and duplicate edges fail.
- Every other line fails with file and line, including `subgraph`, `style`, `linkStyle`, edge labels, other arrow types, chaining (`A --> B --> C`), `&`, and nodes declared inside edges.
- Comparison uses labels, not ids: nodes as a set of labels, edges as a set of (source label, target label) pairs.

```text
%% verify: project-references
flowchart LR
    api["ChronicleOfHeros.Api"]
    contracts["ChronicleOfHeros.Identity.Contracts"]
    api --> contracts
```

### Table grammar

- The header row holds exactly the view's columns, in order. The delimiter row follows (cells match `:?-{3,}:?`). Data rows follow until the first line that does not start with `|`.
- Rows start and end with `|`. Cells are split on `|` (escaped pipes are not supported), trimmed, and stripped of every backtick character.
- A first cell starting with `(added) `, `(removed) `, or `(changed) ` loses that prefix before comparison.
- `-` means none. List cells separate items with `, ` and compare as sets; a duplicate item fails.
- Rows compare as a set by key; a duplicate key fails. Row order is free.

```text
<!-- verify: api-endpoints -->
| Method | Route | Adapter | Authorization |
|---|---|---|---|
| GET | /players/me | CurrentPlayerEndpoint | Policy:Player |
```

### View catalogue

| Id | Kind | Lives in | Key | Required |
|---|---|---|---|---|
| `project-references` | Mermaid | [solution/projects.md](solution/projects.md) | — | always |
| `capability-layers:<capability>` | Mermaid | `capabilities/<capability>/components.md` | — | one per contracts project |
| `aspire-resources` | table | [system/deployment.md](system/deployment.md) | Resource | always |
| `aspire-parameters` | table | [system/deployment.md](system/deployment.md) | Parameter | always |
| `api-endpoints` | table | [hosts/api.md](hosts/api.md) | Method + Route | always |
| `dbcontext-entities:<DbContext>` | table | `capabilities/<capability>/persistence.md` | Entity | one per production `DbContext` |

#### `project-references`

- Nodes: every `<Project Path="…">` in `ChronicleOfHeros.slnx`, at any folder depth. Label: the project file name without `.csproj`.
- Edges: `A --> B` when A's project file has a `<ProjectReference Include="…">` that resolves, relative to A's directory and with `/` and `\` treated alike, to B's project file. A reference to a project outside the solution fails.

#### `capability-layers:<capability>`

- Required for each solution project named `ChronicleOfHeros.<Name>.Contracts`; `<capability>` is `<Name>` in lower case.
- Scope: the assemblies of solution projects named `ChronicleOfHeros.<Name>.*`, except `*.Tests`.
- Types: every type in scope except compiler-generated ones (name contains `<`, or carries `CompilerGeneratedAttribute`). Dependencies of a compiler-generated type (lambda closure, async state machine, file-local type) count for its outermost non-generated declaring type, or are ignored when it has none.
- Nodes: labels are namespaces. A type belongs to the node whose label is the longest prefix of its namespace on `.` boundaries (`A.B` matches `A.B` and `A.B.C`, not `A.BC`). A type without a node, or a node without a type, fails.
- Edges: `X --> Y` when X ≠ Y and a type of X depends on a type of Y in compiled IL (base type, interfaces, member signatures, attributes, generic arguments, method-body references), as ArchUnitNET reports. Dependencies on types outside scope are ignored. Constants and `nameof` compile to literals and create no edge.

#### `aspire-resources`

Columns: `Resource | Declared by | Parent | Project | References | Waits for | Health check`. Extracted from the syntax of `src/ChronicleOfHeros.AppHost/AppHost.cs`:

- Every call named `Add*` except `AddParameter` declares a resource. Resource: its first argument, a string literal. Declared by: the method name.
- Parent: the resource whose builder is the call's receiver; `-` when the receiver is the distributed-application builder.
- Project: for `AddDotnetProject` and `AddProject`, the file name without `.csproj` of the path argument; otherwise `-`.
- A variable stands for the resource or parameter declared by the last `Add*` call in its initializer chain. Calls later in a chain apply to the resource declared by the nearest preceding `Add*` call in that chain.
- References: one item per `WithReference(<x>)`, the name `<x>` stands for.
- Waits for: `<name>` per `WaitFor(<x>)`; `<name> (completion)` per `WaitForCompletion(<x>)`.
- Health check: `http <path>` per `WithHttpHealthCheck`, where `<path>` is the path argument or `/` when omitted.
- Other calls are ignored. Resources and annotations that integrations add implicitly, such as generated passwords or built-in health checks, are out of scope.

#### `aspire-parameters`

Columns: `Parameter | Secret | Consumed as`. Extracted from the same file:

- One row per `AddParameter` call. Parameter: its first argument. Secret: `yes` when the `secret` argument is `true`, otherwise `no`.
- Consumed as: one `<resource>:<VARIABLE>` item per `WithEnvironment("<VARIABLE>", <x>)` where `<x>` stands for the parameter. Passing the parameter to any other call fails until this specification covers it.

#### `api-endpoints`

Columns: `Method | Route | Adapter | Authorization`. Extracted from the syntax of `src/ChronicleOfHeros.Api/**/*.cs`, excluding `bin` and `obj`:

- One row per call to `MapGet`, `MapPost`, `MapPut`, `MapDelete`, or `MapPatch` whose first argument is a string literal. Method: the verb in upper case. Route: the string literals of enclosing `MapGroup` receivers, then the call's literal.
- Adapter: the class that declares the member containing the call (`Program` for top-level statements).
- Authorization, from calls chained on the endpoint or its groups: `Anonymous` for `AllowAnonymous()`, `Policy:<name>` per string argument of `RequireAuthorization(…)`, `Authenticated` for `RequireAuthorization()` without arguments, `None` when there is none of these.
- Shared `MapDefaultEndpoints` routes live in `ChronicleOfHeros.ServiceDefaults` and are out of scope.

#### `dbcontext-entities:<DbContext>`

Columns: `Entity | Table`. Compared with the runtime EF Core model:

- Required for each non-abstract `DbContext` subclass in a production assembly; `<DbContext>` is its class name.
- The test creates the context with the Npgsql provider and a placeholder connection string (the model needs no connection) and reads `Model.GetEntityTypes()`.
- Entity: the CLR type name without namespace; a generic type is written `Name<Arg>`, with C# keyword aliases such as `string`. Table: `GetTableName()`, or `-` when null.

### Failure messages

A failing comparison reports the view id, the file and line of its marker, the items missing from the doc, and the items missing from the code.
