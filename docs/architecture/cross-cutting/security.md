# Cross-cutting: security

Scope: authentication, authorization, token, cookie, antiforgery, and transport-security mechanisms as implemented across hosts.
Load when: changing JWT issuing or validation, signing keys, authorization policies, password rules, cookies, antiforgery, or HTTPS settings.
Parent: [ARCHITECTURE.md](../../../ARCHITECTURE.md) · Neighbours: [localization.md](localization.md), [observability.md](observability.md), [../flows/authentication.md](../flows/authentication.md), [../capabilities/identity/classes.md](../capabilities/identity/classes.md)

## Per host

| Host | Authentication | Authorization | Browser protections |
|---|---|---|---|
| API | JWT bearer (default scheme) | Per-endpoint policies; four endpoints anonymous ([../hosts/api.md](../hosts/api.md)) | None; no cookies, HTTPS redirection, or CORS |
| Web | None | None | Antiforgery middleware; manual antiforgery check on `POST /display-language`; HTTPS redirection; HSTS outside Development |
| Migrations | None | None | Not an HTTP host |

## Access tokens

| Property | Value |
|---|---|
| Format and algorithm | JWT, RS256 |
| Signing key | `Jwt:SigningPrivateKey`, Base64 PKCS#8 RSA private key; the validation key is its public key, derived in-process |
| Issuer, audience | `Jwt:Issuer`, `Jwt:Audience`; both validated |
| Claims | `sub` (account ID), `jti`, `iat`, then one `ClaimTypes.Role` claim per role, or `scope=password-change` for a restricted token |
| Lifetime | 15 minutes normal, 5 minutes restricted; lifetime validated with zero clock skew |
| Inbound mapping | `MapInboundClaims = false`; name claim `sub`; role claim `ClaimTypes.Role` |

Refresh tokens are opaque: 64 random bytes, Base64Url-encoded, stored only as a SHA-256 hash ([../capabilities/identity/state.md](../capabilities/identity/state.md)). Access tokens cannot be revoked; they expire.

## Policies

| Policy | Requirement |
|---|---|
| `Player` | role `Player` |
| `PasswordChange` | role `Player`, or claim `scope` = `password-change` |
| `Operator` | role `Operator` |

Registered by Identity (`ConfigureIdentityPolicies`) and referenced by name in the API adapters.

## Passwords and accounts

- ASP.NET Core Identity hashes passwords. Rules: at least 8 characters, 1 unique character, a digit, a lowercase and an uppercase letter, and a non-alphanumeric character.
- Passwords are checked with `UserManager.CheckPasswordAsync`; there is no lockout, rate limiting, or two-factor authentication.
- Temporary credentials and bootstrap Operator rules: [../capabilities/identity/domain.md](../capabilities/identity/domain.md).

## Cookies

The only cookie the application sets is the display-language preference ([localization.md](localization.md)); it carries no identity. Antiforgery uses ASP.NET Core's default cookie and form token.

## Secrets

The bootstrap temporary password and the JWT signing private key are Aspire secret parameters passed to the API as environment variables ([../system/deployment.md](../system/deployment.md)). Tests generate their own RSA keys.

## Transport

- The API and Web launch profiles declare only `https` URLs (`https://localhost:7043`, `https://localhost:7098`); neither host has a plain-HTTP endpoint or `http` profile. The AppHost therefore gives each project resource a single `https` endpoint ([../system/deployment.md](../system/deployment.md)).
- Web additionally calls `UseHttpsRedirection()` and, outside Development, `UseHsts()`. The API has neither.
- Web receives an `https` service-discovery entry for `api` ([../system/deployment.md](../system/deployment.md)) but makes no calls to the API. No host uses a service-discovery URI.
- The PostgreSQL connection's transport security is not configured by the application.

## Known deviations

- The browser can obtain and present API tokens itself; Web has no session cookie, Redis session store, Data Protection key ring, or Web-side JWT validation. Deviates from [ADR 0004](../../adr/0004-same-origin-web-api-gateway.md) and [CODING_STANDARDS.md § Web and security boundaries](../../../CODING_STANDARDS.md#web-and-security-boundaries): tokens stay server-side behind an opaque `__Host-` session cookie. Tracking: #84.
