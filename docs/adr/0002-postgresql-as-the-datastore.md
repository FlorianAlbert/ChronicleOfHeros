# PostgreSQL as the datastore

Character data needs a persistent, relational store that both `Api` and future migration tooling can depend on for the long term, making the database engine a hard-to-reverse choice. We decided on **PostgreSQL**, provisioned as an Aspire-managed container resource, over SQL Server: it's free of licensing friction in dev/CI, runs natively on Linux without extra tooling, and has strong first-class support in both Aspire's hosting integrations and EF Core.

`Api` owns database access through EF Core with Npgsql and will register its `ChronicleOfHerosDbContext` through Aspire's PostgreSQL integration. This gives the API a connection-string reference through service discovery and PostgreSQL-aware health checks, while entities and migrations remain deferred until the first domain feature needs them.
