using ChronicleOfHeros.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<ChronicleOfHerosDbContext>("chronicleofheros");

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();