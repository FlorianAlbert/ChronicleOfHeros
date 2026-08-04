# Blazor Antiforgery In Interactive Auto

## Question

Can the display-language selector remain a self-contained HTML form in the
interactive layout, with token-based antiforgery protection after an
`InteractiveAuto` hydration?

## Conclusion

Yes. .NET 10 provides a native solution when the not-found page is rendered in
the original Razor component request: keep `<AntiforgeryToken />` in the form
and let its `AntiforgeryStateProvider` supply the persisted request token. No
custom token endpoint, JavaScript fetch, or manually persisted token is
required.

`AddRazorComponents()` registers the server `AntiforgeryStateProvider` and
persists it for `InteractiveAuto`. The WebAssembly host also registers its
`DefaultAntiforgeryStateProvider` and restores persistent state. The framework
`AntiforgeryToken` component obtains its value from that provider and renders
the required hidden form field. This is exactly the selector's normal POST
pattern.

Status-code re-execution is an important exception. The .NET 10
`RazorComponentEndpointInvoker` deliberately skips
`PrerenderPersistedStateAsync` for re-executed responses. A token can therefore
appear in the server-rendered form but disappear when `InteractiveAuto`
hydrates because the interactive runtime did not receive the provider state.

## Recommendation

Keep the selector as one normal `<form method="post">` in `NavMenu`, remove
the token-fetch endpoint and its JavaScript, and retain `<AntiforgeryToken />`.
Route unmatched local URLs through a catch-all component that calls
`NavigationManager.NotFound()`. The configured `Router.NotFoundPage` then
renders the localized page with HTTP 404 in the original component response,
where normal persistent state is emitted.

The selector must inherit the application's global `InteractiveAuto` render
mode; do not force it into a different child render mode. The browser test
should assert that the hidden token remains present after hydration and that
the protected POST returns to the attempted local 404 path.

`AntiforgeryStateProvider` is also the native API for an AJAX/API submission:
obtain its token and send it in a configured request header. That is useful for
a genuine API workflow, but it is unnecessary for this navigation-oriented
HTML form, which requires a browser redirect and already has a supported
hidden-field transport.

## Evidence

- [Blazor security: antiforgery support](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0#antiforgery-support) states that request tokens are stored in component state so they remain available to interactive components without an HTTP request.
- [Blazor forms: antiforgery support](https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/?view=aspnetcore-10.0#antiforgery-support) documents `<AntiforgeryToken />` for normal HTML forms and the `UseAntiforgery` middleware requirement.
- [Call a web API: antiforgery support](https://learn.microsoft.com/en-us/aspnet/core/blazor/call-web-api?view=aspnetcore-10.0#antiforgery-support) documents `AntiforgeryStateProvider` for programmatic HTTP requests.
- [.NET 10 server registration](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Components/Endpoints/src/DependencyInjection/RazorComponentsServiceCollectionExtensions.cs) registers `EndpointAntiforgeryStateProvider` and makes it persistent for `RenderMode.InteractiveAuto`.
- [.NET 10 WebAssembly registration](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Components/WebAssembly/WebAssembly/src/Hosting/WebAssemblyHostBuilder.cs) registers `DefaultAntiforgeryStateProvider` and restores it for interactive WebAssembly.
- [.NET 10 `AntiforgeryToken` source](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Components/Web/src/Forms/AntiforgeryToken.cs) shows the component querying `AntiforgeryStateProvider` and rendering a hidden input from the returned field name and value.
- [.NET 10 `RazorComponentEndpointInvoker` source](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Components/Endpoints/src/RazorComponentEndpointInvoker.cs) excludes error-handler and re-executed responses when emitting persisted component state.

## Caveat

Persistent component state transferred to WebAssembly is visible to the
browser, so it must not be used for secrets. A request antiforgery token is
already intentionally delivered to the browser as a hidden field and is the
framework-supported exception for this form workflow.