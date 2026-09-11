using System.IdentityModel.Tokens.Jwt;

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

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

SignInEndpoint.Map(app);
ChangePasswordEndpoint.Map(app);
RefreshEndpoint.Map(app);
SignOutEndpoint.Map(app);

app.MapPost(
    "/players",
    async (
        EnrollPlayerRequest request,
        IPlayerAdministrationService playerAdministrationService,
        CancellationToken cancellationToken) =>
    {
        return IdentityHttpResults.From(
            await playerAdministrationService.EnrollAsync(request, cancellationToken).ConfigureAwait(false),
            value => Results.Created($"/players/{value.AccountId}", value.TemporaryCredential));
    })
    .RequireAuthorization("Operator");

app.MapPost(
    "/players/{username}/reset-password",
    async (
        string username,
        IPlayerAdministrationService playerAdministrationService,
        CancellationToken cancellationToken) =>
    {
        return IdentityHttpResults.From(
            await playerAdministrationService.ResetPasswordAsync(
                new ResetPasswordRequest(username),
                cancellationToken).ConfigureAwait(false),
            value => Results.Ok(value));
    })
    .RequireAuthorization("Operator");

app.MapGet(
    "/players/me",
    (HttpContext context) =>
    {
        var accountId = Guid.Parse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        return Results.Ok(new PlayerIdentityResponse(accountId));
    })
    .RequireAuthorization("Player");

app.Run();