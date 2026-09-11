using ChronicleOfHeros.Api.Authentication;
using ChronicleOfHeros.Identity.AspNetCore;
using ChronicleOfHeros.Identity.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAspNetCoreIdentity();
builder.Services.AddScoped(services => new SignInEndpoint(
    services.GetRequiredService<IAuthenticationService>()));
builder.Services.AddScoped(services => new ChangePasswordEndpoint(
    services.GetRequiredService<IAuthenticationService>()));
builder.Services.AddScoped(services => new RefreshEndpoint(
    services.GetRequiredService<IAuthenticationService>()));
builder.Services.AddScoped(services => new SignOutEndpoint(
    services.GetRequiredService<IAuthenticationService>()));
builder.Services.AddScoped(services => new EnrollPlayerEndpoint(
    services.GetRequiredService<IPlayerAdministrationService>()));
builder.Services.AddScoped(services => new ResetPlayerPasswordEndpoint(
    services.GetRequiredService<IPlayerAdministrationService>()));
builder.Services.AddScoped(services => new CurrentPlayerEndpoint(
    services.GetRequiredService<IPlayerIdentityService>()));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

SignInEndpoint.Map(app);
ChangePasswordEndpoint.Map(app);
RefreshEndpoint.Map(app);
SignOutEndpoint.Map(app);
EnrollPlayerEndpoint.Map(app);
ResetPlayerPasswordEndpoint.Map(app);
CurrentPlayerEndpoint.Map(app);

app.Run();