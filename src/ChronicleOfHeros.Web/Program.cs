using ChronicleOfHeros.Web.Components;
using ChronicleOfHeros.Web.Services.ServerHealthReportService;
using Microsoft.Extensions.Http.Resilience;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddServerHealthReportService("https+http://api");

builder.Services.AddHttpForwarderWithServiceDiscovery()
                .Configure<HttpStandardResilienceOptions>(typeof(IHttpForwarder).FullName, options =>
                    {
                        options.Retry.MaxRetryAttempts = 0;
                    });

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

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
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(ChronicleOfHeros.Web.Client._Imports).Assembly);

app.MapForwarder("/api/{**catch-all}", "https+http://api", transformBuilder =>
{
    transformBuilder.AddPathRemovePrefix("/api");
});

app.MapDefaultEndpoints();

app.Run();
