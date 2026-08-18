using ChronicleOfHeros.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.AddNpgsqlDbContext<ChronicleOfHerosDbContext>("chronicleofheros");

using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();
var dbContext = scope.ServiceProvider.GetRequiredService<ChronicleOfHerosDbContext>();

await dbContext.Database.MigrateAsync();