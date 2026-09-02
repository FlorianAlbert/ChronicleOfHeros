using System.IdentityModel.Tokens.Jwt;

using ChronicleOfHeros.Api.Authentication;
using ChronicleOfHeros.Identity.AspNetCore;
using ChronicleOfHeros.Identity.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAspNetCoreIdentity();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

app.MapPost(
    "/authentication/sign-in",
    async (
        SignInRequest request,
            IAuthenticationService authenticationService,
        CancellationToken cancellationToken) =>
    {
        return IdentityHttpResults.From(
            await authenticationService.SignInAsync(request, cancellationToken).ConfigureAwait(false),
            value => Results.Ok(value));
    })
    .AllowAnonymous();

app.MapPost(
    "/authentication/change-password",
    async (
        ChangePasswordRequest request,
        HttpContext context,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken) =>
    {
        var accountId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(accountId, out var parsedAccountId))
        {
            return Results.Unauthorized();
        }

        return IdentityHttpResults.From(
            await authenticationService.ChangePasswordAsync(parsedAccountId, request, cancellationToken).ConfigureAwait(false),
            value => Results.Ok(value));
    })
    .RequireAuthorization("PasswordChange");

app.MapPost(
    "/authentication/refresh",
    async (
        RefreshTokenRequest request,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken) =>
    {
        return IdentityHttpResults.From(
        await authenticationService.RefreshAsync(request, cancellationToken).ConfigureAwait(false),
        value => Results.Ok(value));
    })
    .AllowAnonymous();

app.MapPost(
    "/authentication/sign-out",
    async (
        RefreshTokenRequest request,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken) =>
    {
        await authenticationService.SignOutAsync(request, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    })
    .AllowAnonymous();

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