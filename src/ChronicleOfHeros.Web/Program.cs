using ChronicleOfHeros.Web.Components;
using ChronicleOfHeros.Web.Services.Localization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Http.Resilience;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHttpForwarderWithServiceDiscovery()
                .Configure<HttpStandardResilienceOptions>(typeof(IHttpForwarder).FullName, options =>
                    {
                        options.Retry.MaxRetryAttempts = 0;
                    });

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
var supportedCultures = new[] { "en-US", "de-DE" };
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture("en-US")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
    options.RequestCultureProviders =
    [
        new DisplayLanguageRequestCultureProvider(supportedCultures),
    ];
    options.ApplyCurrentCultureToResponseHeaders = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.UseRequestLocalization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(ChronicleOfHeros.Web.Client._Imports).Assembly);

app.MapPost("/display-language", async (HttpContext context, IAntiforgery antiforgery) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(context);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest();
    }

    var form = await context.Request.ReadFormAsync(context.RequestAborted);
    var selectedCulture = supportedCultures.FirstOrDefault(culture =>
        string.Equals(culture, form["locale"], StringComparison.OrdinalIgnoreCase));

    if (selectedCulture is not null)
    {
        context.Response.Cookies.Append(
            DisplayLanguageRequestCultureProvider.PreferenceCookieName,
            selectedCulture,
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(400),
                HttpOnly = true,
                MaxAge = TimeSpan.FromDays(400),
                Path = "/",
                SameSite = SameSiteMode.Lax,
                Secure = true,
            });
    }

    return Results.Redirect(GetSafeLocalReturnPath(form["returnUrl"].ToString()));
});

app.MapForwarder("/api/{**catch-all}", "https+http://api", transformBuilder =>
{
    transformBuilder.AddPathRemovePrefix("/api");
});

app.MapDefaultEndpoints();

app.Run();

static string GetSafeLocalReturnPath(string? returnUrl) =>
    IsSafeLocalReturnPath(returnUrl) ? returnUrl! : "/";

static bool IsSafeLocalReturnPath(string? returnUrl)
{
    if (string.IsNullOrWhiteSpace(returnUrl)
        || returnUrl.Contains('\\')
        || !Uri.TryCreate(returnUrl, UriKind.Relative, out _))
    {
        return false;
    }

    var pathEnd = returnUrl.IndexOfAny(['?', '#']);
    var encodedPath = pathEnd < 0 ? returnUrl : returnUrl[..pathEnd];
    var decodedPath = encodedPath;

    while (true)
    {
        var nextPath = Uri.UnescapeDataString(decodedPath);
        if (nextPath == decodedPath)
        {
            break;
        }

        decodedPath = nextPath;
    }

    return decodedPath[0] == '/'
           && !decodedPath.StartsWith("//", StringComparison.Ordinal)
           && !decodedPath.StartsWith("/\\", StringComparison.Ordinal)
           && !decodedPath.Contains('\\');
}
