# Cross-cutting: security

Scope: authentication, authorization, browser protections, and transport-security mechanisms as implemented across hosts.
Load when: adding authentication or authorization, changing browser protections, or changing HTTPS settings.
Parent: [ARCHITECTURE.md](../../../ARCHITECTURE.md) · Neighbours: [localization.md](localization.md), [observability.md](observability.md), [../hosts/api.md](../hosts/api.md)

## Per host

| Host | Authentication | Authorization | Browser protections |
|---|---|---|---|
| API | None | None | No application endpoints; shared Development health endpoints only |
| Web | None | None | ASP.NET Core automatic CSRF protection (`Sec-Fetch-Site`/`Origin` checks), enforced on `POST /display-language` through form binding; HTTPS redirection; HSTS outside Development |

The API currently has no application authentication, authorization policies, accounts, or protected operations.

## Cookies

The only application cookie is the display-language preference ([localization.md](localization.md)); it carries no identity. CSRF protection is header-based, so there is no antiforgery cookie or form token.

## Transport

- The API and Web launch profiles declare only `https` URLs (`https://localhost:7043`, `https://localhost:7098`); neither host has a plain-HTTP endpoint or `http` profile. The AppHost therefore gives each project resource a single `https` endpoint ([../system/deployment.md](../system/deployment.md)).
- Web calls `UseHttpsRedirection()` and, outside Development, `UseHsts()`. The API has neither.
- Web receives an `https` service-discovery entry for `api` ([../system/deployment.md](../system/deployment.md)) but makes no calls to the API.

## Known deviations

- The API no longer implements the ASP.NET Core Identity/JWT design recorded in [ADR 0006](../../adr/0006-identity-backed-jwt-authentication.md). The browser session and server-held API tokens described by [ADR 0004](../../adr/0004-same-origin-web-api-gateway.md) are also not implemented. Until a replacement is implemented, there are no protected application endpoints.
- The current first-run/development realm import creates a Keycloak user with Player and Operator realm roles from Aspire parameters and requires `UPDATE_PASSWORD`. Only Keycloak receives the secret bootstrap password. This user is not Keycloak's `master`-realm administrator, and assigning realm roles does not implement application account management or authorization. The import skips an existing realm; it is not a tracked, verified production setup.
