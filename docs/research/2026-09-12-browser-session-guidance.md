# ASP.NET Core And Blazor Auto Browser Sessions

## Applicability

This report informs a future browser-authentication capability for Chronicle of
Heros. It does not change the existing JWT API contract or implement
authentication.

The application targets `net11.0`, has a static-SSR-by-default Blazor Web App
with per-component Interactive Auto, and already exposes the API only through
the Web origin's YARP `/api` forwarder. See
[ADR-0003](../adr/0003-blazor-auto-render-mode.md),
[ADR-0004](../adr/0004-same-origin-web-api-gateway.md), and
[ADR-0006](../adr/0006-identity-backed-jwt-authentication.md).

## Question

What is the current ASP.NET Core and Blazor guidance for a browser session when
Interactive Auto components call the same-origin Web host, while the internal
API remains JWT bearer authenticated?

## Conclusion

Adopt a Backend for Frontend (BFF) boundary in the Web host. The browser should
receive only a scoped Web session cookie; the Web host should retain access and
refresh tokens server-side and attach the access token only to its internal API
calls. Microsoft documents this exact architecture for an Interactive Auto
Blazor Web App using YARP: the server owns the authentication cookie and proxy,
and the API remains JWT-bearer protected.

This is a change to the **browser-facing** contract, not a replacement for the
current API contract. The API can continue issuing its 15-minute access JWTs
and rotating opaque refresh-token families. The BFF becomes the API client that
uses those contracts on behalf of an authenticated Player or Operator.

## Recommendation

1. Add a dedicated cookie authentication scheme to `ChronicleOfHeros.Web` and
   run authentication and authorization before its mapped endpoints. Issue a
   host-only, `Secure`, `HttpOnly`, `SameSite=Strict` session cookie with
   `Path=/`; use a session cookie by default and make persistent sign-in an
   explicit Player choice. Cookie middleware decrypts the ticket and populates
   `HttpContext.User`; persistent cookies require explicit consent.

2. Put only a random session identifier and minimal display/authorization
   claims in the Web cookie. Store the current access token, refresh token,
   expiry, and refresh-family/session linkage in a server-side protected
   session store. This stricter token-custody recommendation is a design choice
   for this application: Microsoft samples can save tokens in the encrypted
   authentication ticket, but their overriding guidance is that tokens and
   authentication data must never reach the `.Client` project.

3. Have Web sign-in, refresh, password-change, and sign-out handlers invoke
   the existing API authentication contracts internally. On sign-out, revoke
   the stored refresh-token family through the API before deleting the Web
   session and cookie. Refresh server-side before forwarding a request whose
   access token is expired or near expiry; on refresh failure, delete the
   session and return an unauthenticated result.

4. Replace broad anonymous pass-through of authenticated browser API traffic
   with explicit BFF endpoints or guarded YARP transforms. They must load the
   Web session, set the outgoing `Authorization: Bearer` header themselves, and
   never forward a browser-supplied bearer header or expose the refresh and
   token-pair responses. A scoped `DelegatingHandler` is the documented
   mechanism for server-side API calls; a YARP transform is appropriate for the
   existing proxy route.

5. Require authorization twice for Interactive Auto data: on the rendered
   component/page and on the same-origin Web endpoint it calls. Serialize only
   non-sensitive identity display state to WebAssembly. The client-side
   authentication state is fixed for the lifetime of the loaded application, so
   sign-in, sign-out, role changes, and invalidated sessions should force a
   full reload before the UI relies on the new state.

6. Keep CSRF defenses on every browser-reachable, cookie-authenticated mutation
   endpoint. .NET 11's automatic Fetch-Metadata protection is useful defense in
   depth, but it only becomes a rejection when a form consumer observes the
   recorded verdict; a JSON-binding BFF endpoint needs explicit antiforgery
   validation. Retain `UseAntiforgery()` and have programmatic client calls send
   the configured request-token header; do not call `DisableAntiforgery()` on
   an endpoint authenticated by the Web cookie.

7. Make Web-session revocation explicit. Validate session state when the cookie
   is used, at a bounded interval where necessary, and reject it when the
   backing session is revoked, expired, or no longer matches the stored refresh
   family. The cookie authentication `ValidatePrincipal` hook supports this,
   but Microsoft cautions that validation on every request can be costly.

## Current Contract And Extension Points

- The API's `IAuthenticationService` already provides sign-in, refresh,
  password change, and sign-out operations. Its normal sign-in and refresh
  flows create a JWT access token and an opaque refresh token; refresh rotation
  revokes the full family when a used token is replayed. These are suitable
  internal BFF operations, but their JSON responses must stop being a browser
  token surface.
- A Player who must change their password currently receives a restricted,
  five-minute password-change access token. Model this as a short-lived
  Web-owned pending-password-change session rather than returning that token to
  the browser. Once the password is changed, replace it with the normal Web
  session using the API's resulting token pair.
- API bearer validation stays in the API. The Web host does not validate or
  reinterpret access JWTs for API authorization; it authenticates the browser
  session, then presents the API's existing bearer token to the API.
- The existing Web `UseAntiforgery()` and same-origin `/api` forwarder are
  useful foundations, but neither currently authenticates the browser nor
  injects a bearer token. They need a dedicated capability rather than a
  browser-side token provider.

## Deployment Constraints

For more than one Web instance, use a shared ASP.NET Core Data Protection key
ring and application identifier so all instances can read the session cookie.
The server-side session/token store must also be shared and atomic for refresh
rotation. Interactive Server circuits additionally require session affinity;
the cookie/session design must not assume that circuit affinity substitutes for
shared session storage.

## Evidence

- [Secure a Blazor Web App with OIDC](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-oidc?view=aspnetcore-11.0) documents an Interactive Auto BFF using Aspire and YARP: the Web app stores access tokens in its authentication cookie, YARP proxies to a JWT-bearer API, and server-only token handling is used for API calls.
- [Blazor additional server-side security scenarios](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/additional-scenarios?view=aspnetcore-11.0#pass-tokens-to-a-server-side-blazor-app) says tokens and authentication data must never leave the server or be handled by the `.Client` project, and describes using a server-only `DelegatingHandler` to attach access tokens.
- [Cookie authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-11.0) documents cookie middleware, encrypted tickets, middleware ordering, explicit persistent-cookie consent, `ValidatePrincipal`, and shared Data Protection for multi-instance hosting.
- [CSRF prevention](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-11.0) documents .NET 11 automatic Fetch-Metadata CSRF protection, its deferred enforcement, token-based antiforgery, and why browser-reachable cookie endpoints must not opt out.
- [Blazor security](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-11.0#secure-data-in-blazor-web-apps-with-interactive-auto-rendering) requires authorization of both the Interactive Auto component and the server endpoint that supplies its data.

## Caveats

- `SameSite=Strict` is appropriate for this first-party username/password flow.
  Re-evaluate it before adding cross-site OIDC callbacks, payment returns, or
  other external navigation flows, which may require a narrower temporary
  cookie policy.
- Cookie tickets and persisted component state are browser artifacts. Encryption
  is not a reason to place refresh tokens or other durable credentials there;
  keep the BFF's token records server-side and keep serialized client identity
  data intentionally minimal.
- The recommendation needs capability-level tests for token non-disclosure,
  CSRF rejection, API bearer injection, refresh rotation/replay handling,
  sign-out revocation, and behavior after a full reload in Interactive Auto.