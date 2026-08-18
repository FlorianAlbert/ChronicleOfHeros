using ChronicleOfHeros.Api.Data;
using ChronicleOfHeros.Api.Identity;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<ChronicleOfHerosDbContext>("chronicleofheros");
builder.Services.Configure<BootstrapOperatorOptions>(
	builder.Configuration.GetSection(BootstrapOperatorOptions.ConfigurationSectionName));
builder.Services.AddIdentityCore<ApplicationUser>(options =>
	{
		options.Password.RequiredLength = 8;
		options.Password.RequiredUniqueChars = 1;
		options.Password.RequireDigit = false;
		options.Password.RequireLowercase = false;
		options.Password.RequireNonAlphanumeric = false;
		options.Password.RequireUppercase = false;
	})
	.AddRoles<IdentityRole>()
	.AddEntityFrameworkStores<ChronicleOfHerosDbContext>();

var app = builder.Build();

await app.Services.InitializeBootstrapOperatorAsync(
	app.Lifetime.ApplicationStopping);

app.MapDefaultEndpoints();

app.Run();