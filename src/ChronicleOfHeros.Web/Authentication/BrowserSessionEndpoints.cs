using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;

using ChronicleOfHeros.Identity.Contracts;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;

using StackExchange.Redis;

namespace ChronicleOfHeros.Web.Authentication;

// These types are instantiated by ASP.NET Core dependency injection.
#pragma warning disable CA1812 // Avoid uninstantiated internal classes

internal static class BrowserSessionExtensions
{
    extension(WebApplicationBuilder builder)
    {
        internal WebApplicationBuilder AddBrowserSession()
        {
            string redisConnectionString = builder.Configuration.GetConnectionString("browser-sessions")
                ?? throw new InvalidOperationException("The browser session Redis connection string is required.");
            ConfigurationOptions redisOptions = ConfigurationOptions.Parse(redisConnectionString);
            redisOptions.AbortOnConnectFail = false;
            IConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisOptions);

            _ = builder.Services.AddSingleton(redis);
            _ = builder.Services.AddStackExchangeRedisCache(options => options.ConfigurationOptions = redisOptions);
            _ = Microsoft.AspNetCore.DataProtection.StackExchangeRedisDataProtectionBuilderExtensions
                .PersistKeysToStackExchangeRedis(
                    builder.Services.AddDataProtection(),
                    redis,
                    "data-protection-keys");
            _ = builder.Services.AddOptions<BrowserSessionOptions>()
                .Configure(options => options.ConfigureFrom(builder.Configuration))
                .Validate(options => options.HasRequiredValues(), "Browser session JWT configuration is required.")
                .ValidateOnStart();
            _ = builder.Services.AddSingleton<BrowserSessionStore>();
            _ = builder.Services.AddSingleton<BrowserSessionPrincipalFactory>();
            _ = builder.Services.AddScoped<BrowserSessionCookieEvents>();
            _ = builder.Services.AddHttpClient<IdentityApiClient>(client =>
                client.BaseAddress = new Uri("https+http://api"));
            _ = builder.Services.AddHttpClient<PlayerApiClient>(client =>
                client.BaseAddress = new Uri("https+http://api"));
            _ = builder.Services.AddScoped<BrowserSignInEndpoint>();
            _ = builder.Services.AddScoped<CurrentPlayerBrowserEndpoint>();
            _ = builder.Services.AddAuthentication(BrowserSessionDefaults.AuthenticationScheme)
                .AddCookie(BrowserSessionDefaults.AuthenticationScheme, options =>
                {
                    options.Cookie.Name = BrowserSessionDefaults.CookieName;
                    options.Cookie.HttpOnly = true;
                    options.Cookie.IsEssential = true;
                    options.Cookie.Path = "/";
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.EventsType = typeof(BrowserSessionCookieEvents);
                    options.LoginPath = "/sign-in";
                    options.SlidingExpiration = false;
                });
            _ = builder.Services.AddAuthorizationBuilder()
                .AddPolicy("Player", policy => policy.RequireAuthenticatedUser().RequireRole("Player"));
            _ = builder.Services.AddCascadingAuthenticationState();

            return builder;
        }
    }

    extension(WebApplication app)
    {
        internal WebApplication UseBrowserSessionNavigation()
        {
            _ = app.Use(async (context, next) =>
            {
                if (HttpMethods.IsGet(context.Request.Method)
                    && context.User.Identity?.IsAuthenticated == true
                    && (context.Request.Path == "/" || context.Request.Path == "/sign-in"))
                {
                    context.Response.Redirect("/player");
                    return;
                }

                if (HttpMethods.IsGet(context.Request.Method)
                    && context.Request.Path == "/player"
                    && context.User.Identity?.IsAuthenticated != true)
                {
                    await context.ChallengeAsync(BrowserSessionDefaults.AuthenticationScheme).ConfigureAwait(false);
                    return;
                }

                await next(context).ConfigureAwait(false);
            });

            return app;
        }

        internal WebApplication MapBrowserSessionEndpoints()
        {
            BrowserSignInEndpoint.Map(app);
            CurrentPlayerBrowserEndpoint.Map(app);
            return app;
        }
    }
}

internal static class BrowserSessionDefaults
{
    internal const string AuthenticationScheme = "BrowserSession";
    internal const string CookieName = "__Host-ChronicleOfHeros.Session";
    internal const string SessionIdClaimType = "chronicleofheros:browser-session-id";
}

internal sealed class BrowserSessionCookieEvents(
    BrowserSessionStore browserSessionStore,
    BrowserSessionPrincipalFactory principalFactory)
    : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        string? sessionId = context.Principal?.FindFirstValue(BrowserSessionDefaults.SessionIdClaimType);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            await RejectAsync(context, null).ConfigureAwait(false);
            return;
        }

        BrowserSessionRecord? browserSession = await browserSessionStore.GetAsync(
            sessionId,
            context.HttpContext.RequestAborted).ConfigureAwait(false);
        if (browserSession is null)
        {
            await RejectAsync(context, sessionId).ConfigureAwait(false);
            return;
        }

        ClaimsPrincipal? principal = principalFactory.Create(browserSession.AccessToken);
        if (principal?.Identity is not ClaimsIdentity identity)
        {
            await RejectAsync(context, sessionId).ConfigureAwait(false);
            return;
        }

        identity.AddClaim(new Claim(BrowserSessionDefaults.SessionIdClaimType, sessionId));
        context.ReplacePrincipal(principal);
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.StartsWithSegments("/bff", StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        string returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"/sign-in?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return Task.CompletedTask;
    }

    private async Task RejectAsync(CookieValidatePrincipalContext context, string? sessionId)
    {
        if (sessionId is not null)
        {
            await browserSessionStore.DeleteAsync(sessionId, context.HttpContext.RequestAborted).ConfigureAwait(false);
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(BrowserSessionDefaults.AuthenticationScheme).ConfigureAwait(false);
    }
}

internal sealed class BrowserSignInEndpoint(
    IdentityApiClient identityApiClient,
    BrowserSessionStore browserSessionStore)
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapPost(
            "/sign-in/submit",
            async (
                HttpContext context,
                IAntiforgery antiforgery,
                BrowserSignInEndpoint endpoint,
                CancellationToken cancellationToken) =>
            {
                return await endpoint.HandleAsync(context, antiforgery, cancellationToken).ConfigureAwait(false);
            })
            .AllowAnonymous();
    }

    private async Task<IResult> HandleAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
        }
        catch (AntiforgeryValidationException)
        {
            return TypedResults.BadRequest();
        }

        IFormCollection form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        TokenPairResponse? tokenPair = await identityApiClient.SignInAsync(
            new SignInRequest(form["username"], form["password"]),
            cancellationToken).ConfigureAwait(false);
        if (tokenPair is null)
        {
            string returnUrl = BrowserReturnPaths.GetSafeSignInReturnPath(form["returnUrl"].ToString());
            return Results.Redirect($"/sign-in?error=invalid-credentials&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        string sessionId = await browserSessionStore.CreateAsync(tokenPair, cancellationToken).ConfigureAwait(false);
        ClaimsPrincipal principal = new(new ClaimsIdentity(
            [new Claim(BrowserSessionDefaults.SessionIdClaimType, sessionId)],
            BrowserSessionDefaults.AuthenticationScheme));
        await context.SignInAsync(BrowserSessionDefaults.AuthenticationScheme, principal).ConfigureAwait(false);

        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = BrowserReturnPaths.GetSafeSignInReturnPath(form["returnUrl"].ToString());
        return Results.Empty;
    }
}

internal sealed class CurrentPlayerBrowserEndpoint(
    PlayerApiClient playerApiClient,
    BrowserSessionStore browserSessionStore)
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(
            "/bff/players/me",
            async (
                HttpContext context,
                CurrentPlayerBrowserEndpoint endpoint,
                CancellationToken cancellationToken) =>
            {
                return await endpoint.HandleAsync(context, cancellationToken).ConfigureAwait(false);
            })
            .RequireAuthorization("Player");
    }

    private async Task<Results<Ok<PlayerIdentityResponse>, UnauthorizedHttpResult>> HandleAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        string? sessionId = context.User.FindFirstValue(BrowserSessionDefaults.SessionIdClaimType);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return TypedResults.Unauthorized();
        }

        BrowserSessionRecord? session = await browserSessionStore.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return TypedResults.Unauthorized();
        }

        PlayerIdentityResponse? player = await playerApiClient.GetCurrentPlayerAsync(
            session.AccessToken,
            cancellationToken).ConfigureAwait(false);
        if (player is not null)
        {
            return TypedResults.Ok(player);
        }

        await browserSessionStore.DeleteAsync(sessionId, cancellationToken).ConfigureAwait(false);
        await context.SignOutAsync(BrowserSessionDefaults.AuthenticationScheme).ConfigureAwait(false);
        return TypedResults.Unauthorized();
    }
}

internal sealed class IdentityApiClient(HttpClient httpClient)
{
    internal async Task<TokenPairResponse?> SignInAsync(SignInRequest request, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            "/authentication/sign-in",
            request,
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        _ = response.EnsureSuccessStatusCode();
        TokenPairResponse? responseBody = await response.Content.ReadFromJsonAsync<TokenPairResponse>(
            cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(responseBody?.RefreshToken) ? null : responseBody;
    }
}

internal sealed class PlayerApiClient(HttpClient httpClient)
{
    internal async Task<PlayerIdentityResponse?> GetCurrentPlayerAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/players/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return null;
        }

        _ = response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PlayerIdentityResponse>(
            cancellationToken).ConfigureAwait(false);
    }
}

#pragma warning restore CA1812 // Avoid uninstantiated internal classes
