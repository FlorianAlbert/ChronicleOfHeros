using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ChronicleOfHeros.Web.Client.Services.Localization;
using Microsoft.Extensions.Localization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddLocalization();
builder.Services.AddSingleton<IStringLocalizerFactory, MissingTranslationDiagnosticStringLocalizerFactory>();

await builder.Build().RunAsync();
