using ChronicleOfHeros.Web.Components;
using ChronicleOfHeros.Web.Endpoints;
using ChronicleOfHeros.Web.Extensions;
using ChronicleOfHeros.Web.Services.Localization;

using Microsoft.Extensions.Localization;

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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDisplayLanguageEndpoint();

app.MapDefaultEndpoints();

app.Run();
