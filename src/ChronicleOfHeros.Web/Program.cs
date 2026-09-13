using ChronicleOfHeros.Web.Authentication;
using ChronicleOfHeros.Web.Client.Services.Localization;
using ChronicleOfHeros.Web.Components;
using ChronicleOfHeros.Web.Extensions;
using ChronicleOfHeros.Web.Services.Localization;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Localization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
string[] supportedCultures = ["en-US", "de-DE"];
builder.Services.AddLocalization();
builder.Services.AddSingleton<IStringLocalizerFactory, MissingTranslationDiagnosticStringLocalizerFactory>();
builder.Services.Configure<RequestLocalizationOptions>(
    options => options.ConfigureDisplayLanguages(supportedCultures));
builder.AddBrowserSession();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    _ = app.UseExceptionHandler("/error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    _ = app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseRequestLocalization();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseBrowserSessionNavigation();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(ChronicleOfHeros.Web.Client._Imports).Assembly);

app.MapPost("/display-language", async (HttpContext context, IAntiforgery antiforgery) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest();
    }

    IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
    string? selectedCulture = supportedCultures.FirstOrDefault(culture =>
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

    return Results.Redirect(BrowserReturnPaths.GetSafeLocalReturnPath(form["returnUrl"].ToString()));
});

app.MapBrowserSessionEndpoints();
app.MapDefaultEndpoints();

app.Run();
