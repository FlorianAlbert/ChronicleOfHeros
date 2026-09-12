using ChronicleOfHeros.Api.Authentication;
using ChronicleOfHeros.Identity.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddAspNetCoreIdentity();

builder.AddAuthenticationEndpoints();
builder.AddPlayerEndpoints();

WebApplication app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

app.MapAuthenticationEndpoints();
app.MapPlayerEndpoints();

app.Run();