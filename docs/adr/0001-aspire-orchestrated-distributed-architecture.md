# Aspire-orchestrated distributed architecture

ChronicleOfHeros is being built from the outset as a distributed application rather than a single monolith, since it must be the source of truth for character data served to a separate web frontend. We decided to orchestrate the solution with **Aspire**: a dedicated `ChronicleOfHeros.AppHost` project wires the resources together, `ChronicleOfHeros.ServiceDefaults` centralizes shared telemetry/resilience/health-check configuration, `ChronicleOfHeros.Api` is an ASP.NET Core Minimal API backend, and `ChronicleOfHeros.Web` (+ `ChronicleOfHeros.Web.Client`) is the Blazor frontend.

This decision covers only the skeleton's shape. Individual feature implementation inside `Api` and `Web` (endpoints, pages, business logic) is explicitly out of scope until the skeleton is agreed and scaffolded.
