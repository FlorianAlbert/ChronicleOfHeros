using ChronicleOfHeros.Web.Client.Services.ClientHealthReportService;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddClientHealthReportService(builder.HostEnvironment.BaseAddress);

await builder.Build().RunAsync();
