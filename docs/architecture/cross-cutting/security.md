# Cross-cutting: security

Scope: authentication, authorization, browser protections, and transport-security mechanisms as implemented across hosts.
Load when: adding authentication or authorization, changing browser protections, or changing HTTPS settings.
Parent: [ARCHITECTURE.md](../../../ARCHITECTURE.md) · Neighbours: [localization.md](localization.md), [observability.md](observability.md), [../hosts/api.md](../hosts/api.md)

## Per host

| Host | Authentication | Authorization | Browser protections |
|---|---|---|---|
| API | Keycloak JWT bearer (realm `chronicleofheroes`, audience `chronicleofheroes-api`) | Middleware without policies | No application endpoints; shared Development health endpoints only |
| Web | Cookie session established through Keycloak OpenID Connect (authorization-code flow with PKCE, public client `chronicleofheroes-web`) | Middleware without policies; no page requires authentication | ASP.NET Core automatic CSRF protection (`Sec-Fetch-Site`/`Origin` checks), enforced on `POST /display-language` through form binding and on `POST /authentication/login` and `/logout` through `RequireAntiforgeryTokenAttribute`; HTTPS redirection; HSTS outside Development |

The API has no protected operations yet. Neither host defines authorization policies, and the realm roles carried in tokens are not used for authorization.

## Sign-in and sign-out

- **Sign in:** `POST /authentication/login` challenges the OpenID Connect scheme. Keycloak authenticates the user, runs any required actions (the bootstrap Operator must update their password and complete the realm user profile on first sign-in; username, first name, and last name are required, while email is optional for end users) and redirects to `/signin-oidc`. Web then issues its authentication cookie and redirects to the validated local `returnUrl`.
- **Sign out:** `POST /authentication/logout` signs out of the cookie and the OpenID Connect scheme. Keycloak's end-session endpoint receives the saved `id_token` as hint, ends the Keycloak session, and returns through `/signout-callback-oidc` to `/`.
- **Display name:** inbound claims are not mapped. `preferred_username` is the name claim; the header shows the `name` claim when present.

## Cookies

- The display-language preference cookie ([localization.md](localization.md)) carries no identity.
- The ASP.NET Core authentication cookie (default `.AspNetCore.Cookies` name and options, chunked when large) carries the signed-in principal and, because `SaveTokens = true`, the OIDC tokens, encrypted with the default Data Protection keys. Short-lived OpenID Connect nonce and correlation cookies exist during sign-in.
- CSRF protection is header-based, so there is no antiforgery cookie or form token.

## Transport

- The API and Web launch profiles declare only `https` URLs (`https://localhost:7043`, `https://localhost:7098`); neither host has a plain-HTTP endpoint or `http` profile. The AppHost therefore gives each project resource a single `https` endpoint ([../system/deployment.md](../system/deployment.md)).
- Web calls `UseHttpsRedirection()` and, outside Development, `UseHsts()`. The API has neither.
- Web receives `https` service-discovery entries for `keycloak` and `api` ([../system/deployment.md](../system/deployment.md)) but makes no calls to the API. Both hosts resolve the Keycloak authority over HTTPS only.

## Known deviations

- The API no longer implements the ASP.NET Core Identity/JWT design recorded in [ADR 0006](../../adr/0006-identity-backed-jwt-authentication.md); Keycloak is the identity provider. The Web cookie session keeps OIDC tokens in the encrypted cookie instead of the opaque Redis-backed session with server-held API tokens described by [ADR 0004](../../adr/0004-same-origin-web-api-gateway.md). Tracking: #96.
- The current first-run/development realm import creates a Keycloak user with Player and Operator realm roles from Aspire parameters and requires `UPDATE_PASSWORD`. Only Keycloak receives the secret bootstrap password. This user is not Keycloak's `master`-realm administrator, and assigning realm roles does not implement application account management or authorization. The import skips an existing realm; it is not a tracked, verified production setup.
