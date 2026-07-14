# PostgreSQL as the datastore

Character data needs a persistent, relational store that both `Api` and future migration tooling can depend on for the long term, making the database engine a hard-to-reverse choice. We decided on **PostgreSQL**, provisioned as an Aspire-managed container resource, over SQL Server: it's free of licensing friction in dev/CI, runs natively on Linux without extra tooling, and has strong first-class support in both Aspire's hosting integrations and EF Core.

At this skeleton stage, `Api` only receives a reference to the database (connection string via Aspire service discovery) — no `DbContext`, entities, or migrations exist yet. Those arrive when the first feature needs them.
