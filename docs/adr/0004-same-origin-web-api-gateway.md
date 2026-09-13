# Same-origin Web backend for frontend gateway

The browser cannot use Aspire service discovery to call `Api`, and exposing
`Api` directly would require a public endpoint, CORS policy, and a
browser-facing bearer-token contract. `ChronicleOfHeros.Web` is the sole
browser-facing origin and acts as a backend for frontend (BFF). `Api` remains
an internal JWT-bearer API.

The browser holds only an opaque, host-only, `Secure`, `HttpOnly`,
`SameSite=Strict`, `__Host-`-prefixed session cookie. Its value is an opaque
session identifier, not an access token, refresh token, or authentication
claims. Web stores the session record in private Redis. The record contains the
API access and refresh tokens, expiry metadata, refresh-family linkage, and
the minimum server-side identity state required for Web authorization. Token
values are protected with ASP.NET Core Data Protection before storage.

Web validates the Redis session on every authenticated request. A revoked,
expired, or missing session is rejected immediately. Web validates its
server-held access JWT with the API-provisioned public signing key before using
it to establish the browser-session principal. The API independently validates
the bearer token and remains the authority for API authorization.

Web exposes capability-owned browser adapters rather than a blanket
`/api/{**catch-all}` forwarder. Each adapter owns one browser route and calls a
specific API contract. It loads the Web session, refreshes an expired or
near-expiry access token under the session's atomic Redis lock, sets
`Authorization: Bearer` itself, and removes token-pair data from every browser
response. Browser-supplied Authorization headers are ignored. The API
authentication, refresh, and sign-out endpoints are never browser-proxied.

Browser sign-in, sign-out, and password-change flows are static SSR form posts
that redirect with `303 See Other`. All browser-reachable mutations require
antiforgery validation. A Player required to change their password receives a
short-lived, Web-owned pending-password-change session; the restricted API
token never reaches the browser. On sign-out, Web calls the API to revoke the
refresh-token family, deletes the Redis session, and clears the session cookie.

This preserves a stable same-origin browser surface while keeping API tokens
server-side. It also permits explicit authorization at both the browser adapter
and the API without granting the browser direct access to internal services.