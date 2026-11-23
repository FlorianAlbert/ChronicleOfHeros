# CSRF Protection In ASP.NET Core / .NET 11

## Question

What is new about CSRF / antiforgery protection in ASP.NET Core in .NET 11
(GA announced 2026-11-10), and how does it interact with token-based
antiforgery, Blazor Web Apps with static SSR forms, and same-origin
reverse-proxy / gateway setups?

## Conclusion

.NET 11 adds an **automatic, header-based CSRF protection middleware**
(Fetch Metadata `Sec-Fetch-Site` + `Origin`, comparable to Go 1.25
`http.CrossOriginProtection`). It needs no registration: it is **on by default**
in any app built with `WebApplication.CreateBuilder` that has endpoints. It is
additive to token antiforgery, which is **not deprecated** but is now optional
for most apps. There is no dedicated "trusted origins" option; trust comes from
the endpoint's resolved **CORS policy** (only if it uses `.AllowCredentials()`),
or from a custom `ICsrfProtection`.

Caveats worth remembering:

- A denied verdict only turns into HTTP 400 when something **consumes a form**
  (MVC antiforgery actions, minimal-API `[FromForm]`, Blazor SSR form post).
  A direct `Request.ReadFormAsync()` throws instead, which surfaces as a 500.
  JSON endpoints are not rejected.
- Same-origin detection for legacy browsers (no `Sec-Fetch-Site`) compares
  `Origin` with `request.Scheme://request.Host`, so it depends on correct
  scheme/Host at the backend (see section 4).
- Primary sources do not document forwarded-headers or YARP behavior
  explicitly; see "Gaps".

## 1. What was added

| Item | Value | Source |
|---|---|---|
| Mechanism | Auto-injected middleware that inspects `Sec-Fetch-Site` and `Origin` and records a verdict | [Migrate .NET 10 to 11 - Security](https://learn.microsoft.com/en-us/aspnet/core/migration/100-to-110?view=aspnetcore-11.0#automatic-csrf-protection) |
| Public API | `Microsoft.AspNetCore.Antiforgery.ICsrfProtection` (`ValueTask<CsrfProtectionResult> ValidateAsync(HttpContext)`) and `CsrfProtectionResult` (sealed class: `IsAllowed`, `Allowed()`, `Denied()`) | [ICsrfProtection.cs](https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http.Abstractions/src/Antiforgery/ICsrfProtection.cs), [CsrfProtectionResult.cs](https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http.Abstractions/src/Antiforgery/CsrfProtectionResult.cs) |
| Package/assembly | Interfaces live in `Microsoft.AspNetCore.Http.Abstractions` (not the Antiforgery package, to avoid JIT assembly-load problems in `CreateBuilder`); implementation is internal in `Microsoft.AspNetCore` (DefaultBuilder). Namespace stays `Microsoft.AspNetCore.Antiforgery` | [PR #66585](https://github.com/dotnet/aspnetcore/pull/66585) |
| Default implementation | `internal sealed class DefaultCsrfProtection`, registered with `services.TryAddSingleton<ICsrfProtection, DefaultCsrfProtection>()` | [WebHost.cs @ v11.0.0](https://github.com/dotnet/aspnetcore/blob/v11.0.0/src/DefaultBuilder/src/WebHost.cs) (lines 278-279) |
| Middleware | `internal CsrfProtectionMiddleware`; no public `AddCsrfProtection()` / `UseCsrfProtection()` | [CsrfProtectionMiddleware.cs](https://github.com/dotnet/aspnetcore/blob/main/src/DefaultBuilder/src/Internal/CsrfProtectionMiddleware.cs), [PR #66585](https://github.com/dotnet/aspnetcore/pull/66585) ("no `AddCsrfProtection()` or `UseCsrfProtection()`") |
| Origin | Tracking issue / API proposal [dotnet/aspnetcore#65127](https://github.com/dotnet/aspnetcore/issues/65127) cites Go 1.25 `CrossOriginProtection`; shipped in 11.0-preview6 via PR #66585 |  |

### Registration and wiring

- Services: nothing to call. `ICsrfProtection` is registered by the web
  defaults ([WebHost.cs @ v11.0.0](https://github.com/dotnet/aspnetcore/blob/v11.0.0/src/DefaultBuilder/src/WebHost.cs)).
- Middleware: `WebApplicationBuilder` injects it after routing, next to the
  implicit authentication/authorization middleware. It is skipped when
  `DisableCsrfProtection` is true, when `ICsrfProtection` is not registered,
  or when the app has no endpoint data sources
  ([WebApplicationBuilder.cs, lines 457-499](https://github.com/dotnet/aspnetcore/blob/main/src/DefaultBuilder/src/WebApplicationBuilder.cs)).
  Docs: "runs after authentication and authorization"
  ([anti-request-forgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-11.0#automatic-csrf-protection-in-aspnet-core)).
- It runs only for endpoints whose metadata implements `IAntiforgeryMetadata`
  with `RequiresValidation == true` (CsrfProtectionMiddleware.cs above). The
  framework sets that for all Blazor SSR endpoints, minimal APIs that bind form
  data, and MVC actions using antiforgery validation; plain JSON `MapPost` /
  `[HttpPost]` Web API actions have no behavioral change
  ([migration guide](https://learn.microsoft.com/en-us/aspnet/core/migration/100-to-110?view=aspnetcore-11.0#automatic-csrf-protection)).

### Algorithm (shipped, v11.0.0)

Order, first match wins
([anti-request-forgery, "How it works"](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-11.0#automatic-csrf-protection-in-aspnet-core);
[DefaultCsrfProtection.cs](https://github.com/dotnet/aspnetcore/blob/v11.0.0/src/DefaultBuilder/src/Internal/DefaultCsrfProtection.cs)):

1. Safe method (`GET`, `HEAD`, `OPTIONS`, `TRACE`, plus `QUERY` - see
   [PR #67839](https://github.com/dotnet/aspnetcore/pull/67839)) -> allow.
2. `Sec-Fetch-Site` is `same-origin` or `none` -> allow.
3. `Origin` present and the endpoint's resolved CORS policy is not
   `AllowAnyOrigin`, has `SupportsCredentials` (`.AllowCredentials()`), and
   `IsOriginAllowed(origin)` -> allow
   ([PR #69345](https://github.com/dotnet/aspnetcore/pull/69345), merged into
   11.0-rc2).
4. Any other non-empty `Sec-Fetch-Site` (`same-site`, `cross-site`) -> deny.
5. No `Sec-Fetch-Site`, `Origin` present -> compare to
   `"{request.Scheme}://{request.Host.Value}"` (ordinal-ignore-case); match
   allow, otherwise deny.
6. Neither header -> allow (treated as a non-browser client).

### Options: trusted origins and bypass

- **No dedicated trusted-origin option exists.** The initial design in
  [PR #66585](https://github.com/dotnet/aspnetcore/pull/66585) read trusted
  origins from the default CORS policy and mentioned a later separate option;
  the shipped behavior is per-endpoint CORS policy resolution
  (`[EnableCors]`/`RequireCors` named policy, else default policy), and
  requires `.AllowCredentials()`
  ([anti-request-forgery, "Allowing cross-origin clients"](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-11.0#automatic-csrf-protection-in-aspnet-core)).
- `SetIsOriginAllowed` predicates and wildcard subdomains are honored if the
  policy also allows credentials (same page; docs warn about subdomain
  takeover).
- `AllowAnyOrigin` is never a CSRF trust signal; `[DisableCors]` is not a CSRF
  opt-out (same page).
- Custom logic: register your own `ICsrfProtection` with
  `builder.Services.AddSingleton<ICsrfProtection, MyImpl>()` (override, since
  the default uses `TryAddSingleton`). The middleware still honors
  `DisableAntiforgery()` / `[IgnoreAntiforgeryToken]` before calling
  `ValidateAsync` (same page).
- Per-endpoint opt-out: `.DisableAntiforgery()` (minimal APIs) or
  `[IgnoreAntiforgeryToken]` (MVC) - this opts out of **both** token and
  header-based protection (same page). Blazor page: `@attribute
  [RequireAntiforgeryToken(false)]`
  ([migration guide](https://learn.microsoft.com/en-us/aspnet/core/migration/100-to-110?view=aspnetcore-11.0#automatic-csrf-protection)).
- Global disable: configuration key `DisableCsrfProtection` = `true`
  (appsettings or env var such as `ASPNETCORE_DisableCsrfProtection=true`).
  `ICsrfProtection` stays registered
  ([anti-request-forgery, "Disabling globally"](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-11.0#automatic-csrf-protection-in-aspnet-core);
  [Blazor security](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-11.0#antiforgery-support);
  code: [WebApplicationBuilder.cs, line 477](https://github.com/dotnet/aspnetcore/blob/main/src/DefaultBuilder/src/WebApplicationBuilder.cs)).
  Note: an early PR #66585 draft described a `CrossOriginProtection: disable`
  key; the shipped key is `DisableCsrfProtection`.

### Deferred verdict ("record, don't reject")

The middleware never short-circuits. It sets `IAntiforgeryValidationFeature`
(valid or invalid) and logs a Debug message
(`Microsoft.AspNetCore.Antiforgery.CsrfProtectionMiddleware`, event
`CsrfValidationFailed`). Consumers that return 400 on an invalid verdict: MVC
antiforgery filter, minimal-API form binding
([RequestDelegateFactory.cs @ v11.0.0](https://github.com/dotnet/aspnetcore/blob/v11.0.0/src/Http/Http.Extensions/src/RequestDelegateFactory.cs)),
and `RazorComponentEndpointInvoker` (Blazor SSR). A direct
`Request.ReadFormAsync()`/`Request.Form` read instead throws
`InvalidOperationException` ("This form is being accessed with a failed
antiforgery validation"), which surfaces as a 500 unless handled
([FormFeature.cs @ v11.0.0](https://github.com/dotnet/aspnetcore/blob/v11.0.0/src/Http/Http/src/Features/FormFeature.cs))
([PR #67082](https://github.com/dotnet/aspnetcore/pull/67082);
[anti-request-forgery, "Deferred validation"](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-11.0#automatic-csrf-protection-in-aspnet-core)).
An endpoint that never reads the form (e.g., JSON body) runs even with an
invalid verdict.

## 2. Interaction with token-based antiforgery

- **Not deprecated, not replaced; additive.** "This middleware is additive to
  the token-based antiforgery system. The two protections coexist"
  ([anti-request-forgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-11.0#automatic-csrf-protection-in-aspnet-core)).
  The docs also say the token system "remains available" and "for most apps,
  the automatic protection is enough on its own" (same page).
- **Recommended direction**: "the automatic CSRF protection is the recommended
  defense, and most apps no longer need the token-based antiforgery system"
  ([migration guide](https://learn.microsoft.com/en-us/aspnet/core/migration/100-to-110?view=aspnetcore-11.0#automatic-csrf-protection)).
- **Keep tokens when**: the app must support browsers that don't send
  `Sec-Fetch-Site`; it uses `IAntiforgeryAdditionalDataProvider`; or a
  compliance/security review requires tokens as an independent layer (same
  page).
- **Dropping tokens**: removing `AddAntiforgery()` / `UseAntiforgery()` is "a
  one-line change" for most apps (migration guide). Consequence for Blazor
  SSR: without `UseAntiforgery()`, the endpoint no longer generates tokens, so
  `<AntiforgeryToken />` / hidden `__RequestVerificationToken` fields and the
  antiforgery cookie are no longer emitted
  ([breaking change](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/11/blazor-server-side-rendering-deferred-cross-site-request-forgery-protection)).
  Token generation also depends on Data Protection; the header-based approach
  "requires no tokens, no state" (comparison table in
  [anti-request-forgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-11.0#automatic-csrf-protection-in-aspnet-core)).
- **When both run**: `UseAntiforgery()` executes after the header-based
  middleware, clears its verdict, and the **token result is authoritative**
  (valid token can rescue a header-denied request; a missing/invalid token
  rejects a header-allowed one) (same page; [PR #67082](https://github.com/dotnet/aspnetcore/pull/67082)).
- **Registration vs middleware**: `AddAntiforgery` / `AddRazorComponents` /
  `AddMvc` / `MapRazorPages` only register services; Blazor and minimal APIs
  need `UseAntiforgery()` to add the token middleware
  ([anti-request-forgery note, 11.0](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-11.0)).
- **Safety check**: if an endpoint requires antiforgery and neither middleware
  ran (for example `DisableCsrfProtection=true` without `UseAntiforgery()`, or
  a non-`WebApplication` host), the request throws; the docs tell you to call
  `app.UseAntiforgery()` in that configuration
  ([anti-request-forgery, "Disabling globally"](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-11.0#automatic-csrf-protection-in-aspnet-core);
  [PR #67082 notes](https://github.com/dotnet/aspnetcore/pull/67082)).
- Template: `app.UseAntiforgery()` was removed from the Blazor Web App
  templates ([PR #67119](https://github.com/dotnet/aspnetcore/pull/67119);
  [What's new](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-11?view=aspnetcore-11.0):
  "Antiforgery middleware (`app.UseAntiforgery()`) optional in Blazor Web
  Apps"). I verified the v11.0.0 `BlazorWebCSharp.1/Program.cs` template contains
  no `Antiforgery` reference
  ([Program.cs @ v11.0.0](https://github.com/dotnet/aspnetcore/blob/v11.0.0/src/ProjectTemplates/Web.ProjectTemplates/content/BlazorWeb-CSharp/BlazorWebCSharp.1/Program.cs)).
  `AddRazorComponents()` still registers token services
  ([Blazor security](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-11.0#antiforgery-support)).
- The Blazor docs scope `AntiforgeryToken` and `AntiforgeryStateProvider`
  guidance to apps that explicitly call `UseAntiforgery()` (same Blazor
  security page).

## 3. Blazor and proxy scenarios

**Blazor static SSR forms.** All SSR endpoints require validation by default.
Form posts are enforced by the Razor Components endpoint, which trusts the
verdict on `IAntiforgeryValidationFeature` and returns 400 only if invalid; it
no longer validates tokens itself
([breaking change](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/11/blazor-server-side-rendering-deferred-cross-site-request-forgery-protection)).
Same-origin posts pass via `Sec-Fetch-Site: same-origin`.

**Interactive Server.** I found no primary source describing CSRF behavior for
the SignalR circuit/WebSocket traffic itself. Inference only: the middleware
validates only endpoints carrying `IAntiforgeryMetadata`, and the verdict is
enforced only by form consumers, so interactive event handlers are not form
posts. Not verified from docs; treat as uncertain.

**Auth callbacks.** `form_post` OIDC/WS-Fed callbacks are cross-site by
nature (`Sec-Fetch-Site: cross-site`). [PR #68669](https://github.com/dotnet/aspnetcore/pull/68669)
(merged for 11.0-rc1) suppresses the CSRF verdict for remote authentication
callbacks while the handler owns the request (the handler has its own
correlation/state protection). Relevant if the app uses `AddOpenIdConnect`
with `form_post`. Passkey endpoint fix:
[PR #67589](https://github.com/dotnet/aspnetcore/pull/67589).

**Same-origin gateway / reverse proxy to a backend API (cookie or JWT).**
Not covered by primary docs. What the code implies:

- Browser to gateway on the same origin: browser sends
  `Sec-Fetch-Site: same-origin`; the gateway's own middleware allows it. If
  the gateway forwards the header unchanged, the backend also sees
  `same-origin`. Unverified for any specific proxy; headers are
  forwarded by default by typical HTTP proxies but not documented here.
- Backend API with JSON bodies: never rejected by this middleware, because
  nothing consumes a form and these endpoints lack antiforgery metadata
  ([migration guide](https://learn.microsoft.com/en-us/aspnet/core/migration/100-to-110?view=aspnetcore-11.0#automatic-csrf-protection)).
  Docs say endpoints using non-cookie auth such as bearer tokens can opt out
  with `DisableAntiforgery()`.
- Server-to-server calls from a gateway that send neither `Sec-Fetch-Site` nor
  `Origin` are allowed (step 6).
- If the gateway strips `Sec-Fetch-Site` but forwards `Origin`, the backend
  falls to the `Origin` vs `Scheme://Host` comparison (step 5), which fails when
  the backend's `Host`/scheme differ from the public origin.

## 4. Requirements and limitations

- **Browser support**: docs say all current Chromium, Firefox, and Safari send
  `Sec-Fetch-Site`; defers to MDN for the table
  ([anti-request-forgery, "Browser support"](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-11.0#automatic-csrf-protection-in-aspnet-core)).
  Issue #65127 states broad availability since March 2023
  ([issue](https://github.com/dotnet/aspnetcore/issues/65127)).
- **Fallback**: no `Sec-Fetch-Site` -> compare `Origin` to
  `scheme://host[:port]` built from the request (step 5). Docs say this covers
  pre-2020 browsers.
- **HTTP vs HTTPS**: the framework docs do not discuss this. The original
  proposal says `Sec-Fetch-Site` secures sites on "trustworthy (HTTPS or
  localhost) origins" and that plain HTTP origins fall through to the
  Origin/Host comparison
  ([issue #65127](https://github.com/dotnet/aspnetcore/issues/65127)). Browsers
  do not send Fetch Metadata headers to untrustworthy origins (my background
  knowledge of the Fetch Metadata spec, not from the primary ASP.NET docs).
- **Safe methods**: `GET`, `HEAD`, `OPTIONS`, `TRACE`, `QUERY` are never checked.
  Your app must not change state on them.
- **Non-browser clients**: allowed when neither header is present. A
  non-browser client that sends a spoofed `Origin` is still subject to checks,
  but CSRF is browser-only so this does not matter for security.
- **Behind proxies**: step 5 uses `request.Scheme` and `request.Host`
  ([DefaultCsrfProtection.cs @ v11.0.0](https://github.com/dotnet/aspnetcore/blob/v11.0.0/src/DefaultBuilder/src/Internal/DefaultCsrfProtection.cs),
  lines ~109-128). Nothing in the code or docs mentions `X-Forwarded-*`
  explicitly; the values are whatever the pipeline has set (for example by
  `UseForwardedHeaders`). This is code-derived inference, not documented
  guidance.
- **Scope**: protects form-consuming endpoints only; JSON endpoints are not
  rejected automatically.
- **Not a trust list**: CORS `AllowCredentials()` is the trust signal; plain
  `WithOrigins(...)` without credentials does not grant CSRF trust.

## 5. Breaking changes and migration

- [Blazor SSR defers antiforgery validation to middleware](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/11/blazor-server-side-rendering-deferred-cross-site-request-forgery-protection)
  (behavioral). Apps that call `UseAntiforgery()` are unchanged; apps without
  it are now protected by the header-based middleware and stop emitting tokens.
  If you rely on tokens, call `app.UseAntiforgery()`.
- Cross-origin form posts that were previously accepted now get `400 Bad Request`
  unless the origin is trusted through CORS with credentials or the endpoint
  opts out ([migration guide](https://learn.microsoft.com/en-us/aspnet/core/migration/100-to-110?view=aspnetcore-11.0#automatic-csrf-protection)).
- `[ValidateAntiForgeryToken]`/`[AutoValidateAntiforgeryToken]` MVC actions are
  also covered by the header check (migration guide).
- Related fixes after preview: QUERY safe method (#67839), OIDC callback
  suppression (#68669), CORS credentials requirement (#69345).
- Migration page `learn.microsoft.com/.../migration/antiforgery-to-csrf` was
  surfaced by a search engine (pt-br) but returns 404 on en-us and pt-br when
  fetched; do not rely on it.

## Gaps and uncertainties

- The ".NET 11 announcement" blog post
  ([Announcing .NET 11](https://devblogs.microsoft.com/dotnet/announcing-dotnet-11/))
  has no CSRF mention in the page text I fetched; the primary announcement is the
  docs and PRs above.
- "What's new in ASP.NET Core in .NET 11" mentions CSRF only in the
  "`app.UseAntiforgery()` optional in Blazor Web Apps" item.
- No primary docs on YARP / reverse-proxy / forwarded-headers interaction;
  conclusions in sections 3-4 are code-derived inferences.
- No primary docs on Interactive Server (SignalR) behavior.
- HTTP vs HTTPS behavior is documented only in the API proposal issue, not in
  the final docs.
- I did not confirm the exact `PublicAPI.Shipped.txt` entries
  (code search shows `ICsrfProtection` listed in
  `src/Http/Http.Abstractions/src/PublicAPI.Shipped.txt`).

## Evidence

- [What's new in ASP.NET Core in .NET 11](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-11?view=aspnetcore-11.0)
- [Migrate from ASP.NET Core 10 to 11 - Security](https://learn.microsoft.com/en-us/aspnet/core/migration/100-to-110?view=aspnetcore-11.0)
- [Prevent CSRF attacks - automatic CSRF protection](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-11.0#automatic-csrf-protection-in-aspnet-core)
  (source: [AspNetCore.Docs `anti-request-forgery.md`](https://github.com/dotnet/AspNetCore.Docs/blob/main/aspnetcore/security/anti-request-forgery.md))
- [Blazor security - antiforgery support](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-11.0#antiforgery-support)
- [Breaking change: Blazor SSR defers antiforgery validation](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/11/blazor-server-side-rendering-deferred-cross-site-request-forgery-protection)
- dotnet/aspnetcore: [issue #65127](https://github.com/dotnet/aspnetcore/issues/65127),
  PRs [#66585](https://github.com/dotnet/aspnetcore/pull/66585),
  [#67082](https://github.com/dotnet/aspnetcore/pull/67082),
  [#67119](https://github.com/dotnet/aspnetcore/pull/67119),
  [#67839](https://github.com/dotnet/aspnetcore/pull/67839),
  [#68669](https://github.com/dotnet/aspnetcore/pull/68669),
  [#69345](https://github.com/dotnet/aspnetcore/pull/69345)
- Source (tag `v11.0.0`): [DefaultCsrfProtection.cs](https://github.com/dotnet/aspnetcore/blob/v11.0.0/src/DefaultBuilder/src/Internal/DefaultCsrfProtection.cs),
  [WebHost.cs](https://github.com/dotnet/aspnetcore/blob/v11.0.0/src/DefaultBuilder/src/WebHost.cs)
- Source (main): [CsrfProtectionMiddleware.cs](https://github.com/dotnet/aspnetcore/blob/main/src/DefaultBuilder/src/Internal/CsrfProtectionMiddleware.cs),
  [WebApplicationBuilder.cs](https://github.com/dotnet/aspnetcore/blob/main/src/DefaultBuilder/src/WebApplicationBuilder.cs),
  [ICsrfProtection.cs](https://github.com/dotnet/aspnetcore/blob/main/src/Http/Http.Abstractions/src/Antiforgery/ICsrfProtection.cs)
- [Announcing .NET 11](https://devblogs.microsoft.com/dotnet/announcing-dotnet-11/)
