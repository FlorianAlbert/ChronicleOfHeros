using ChronicleOfHeros.Web.Client.Services.Localization;

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Localization;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddLocalization();
builder.Services.AddSingleton<IStringLocalizerFactory, MissingTranslationDiagnosticStringLocalizerFactory>();

await builder.Build().RunAsync().ConfigureAwait(false);
