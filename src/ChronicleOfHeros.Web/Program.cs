using ChronicleOfHeros.Web.Components;
using ChronicleOfHeros.Web.Endpoints;
using ChronicleOfHeros.Web.Extensions;
using ChronicleOfHeros.Web.Services.Localization;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
string[] supportedCultures = ["en-US", "de-DE"];
builder.Services.AddLocalization();
builder.Services.AddSingleton<IStringLocalizerFactory, MissingTranslationDiagnosticStringLocalizerFactory>();
builder.Services.Configure<RequestLocalizationOptions>(
    options => options.ConfigureDisplayLanguages(supportedCultures));

builder.AddDisplayLanguageEndpoint(supportedCultures);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddKeycloakOpenIdConnect(
        serviceName: "keycloak",
        realm: "chronicleofheroes",
        options =>
        {
            // The default "https+http://" authority fails the HTTPS metadata check; resolve HTTPS only.
            options.Authority = "https://keycloak/realms/chronicleofheroes";
            options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.ClientId = "chronicleofheroes-web";
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.Scope.Add("chronicleofheroes:all");
            // Keeps the id_token for the Keycloak end-session request on sign-out.
            options.SaveTokens = true;
            options.MapInboundClaims = false;
            options.TokenValidationParameters.NameClaimType = "preferred_username";
        });
builder.Services.AddAuthorizationBuilder();
builder.Services.AddCascadingAuthenticationState();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
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

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDisplayLanguageEndpoint();
app.MapAuthenticationEndpoints();

app.MapDefaultEndpoints();

app.Run();
