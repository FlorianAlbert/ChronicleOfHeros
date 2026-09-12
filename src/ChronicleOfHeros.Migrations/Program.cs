using ChronicleOfHeros.Identity.AspNetCore;

using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddAspNetCoreIdentity(options => options.EnableMigrations());

await builder.Build().RunAsync().ConfigureAwait(false);