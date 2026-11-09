# Aspire-orchestrated distributed architecture

Status: Accepted

ChronicleOfHeros is being built from the outset as a distributed application rather than a single monolith, since it must be the source of truth for character data served to a separate web frontend. We decided to orchestrate the solution with **Aspire**: a dedicated `ChronicleOfHeros.AppHost` project wires the resources together, `ChronicleOfHeros.ServiceDefaults` centralizes shared telemetry/resilience/health-check configuration, `ChronicleOfHeros.Api` is an ASP.NET Core Minimal API backend, and `ChronicleOfHeros.Web` is the Blazor frontend.

This decision covers only the skeleton's shape; feature work follows the architecture-governance process in [CODING_STANDARDS.md](../../CODING_STANDARDS.md#architecture-governance).
