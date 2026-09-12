using System.IdentityModel.Tokens.Jwt;

using ChronicleOfHeros.Identity.Contracts;

namespace ChronicleOfHeros.Api.Authentication;

internal static class PlayerEndpointsExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddPlayerEndpoints()
        {
            _ = builder.Services.AddScoped(services => new EnrollPlayerEndpoint(
                services.GetRequiredService<IPlayerAdministrationService>()));
            _ = builder.Services.AddScoped(services => new ResetPlayerPasswordEndpoint(
                services.GetRequiredService<IPlayerAdministrationService>()));
            _ = builder.Services.AddScoped(services => new CurrentPlayerEndpoint(
                services.GetRequiredService<IPlayerIdentityService>()));

            return builder;
        }
    }

    extension(WebApplication webApplication)
    {
        public WebApplication MapPlayerEndpoints()
        {
            EnrollPlayerEndpoint.Map(webApplication);
            ResetPlayerPasswordEndpoint.Map(webApplication);
            CurrentPlayerEndpoint.Map(webApplication);
            return webApplication;
        }
    }
}

internal sealed class EnrollPlayerEndpoint(IPlayerAdministrationService playerAdministrationService)
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapPost(
            "/players",
            async (
                EnrollPlayerRequest request,
                EnrollPlayerEndpoint endpoint,
                CancellationToken cancellationToken) =>
            {
                return await endpoint.HandleAsync(request, cancellationToken).ConfigureAwait(false);
            })
            .RequireAuthorization("Operator");
    }

    private async Task<IResult> HandleAsync(EnrollPlayerRequest request, CancellationToken cancellationToken) =>
        IdentityHttpResults.From(
            await playerAdministrationService.EnrollAsync(request, cancellationToken).ConfigureAwait(false),
            value => Results.Created($"/players/{value.AccountId}", value.TemporaryCredential));
}

internal sealed class ResetPlayerPasswordEndpoint(IPlayerAdministrationService playerAdministrationService)
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapPost(
            "/players/{username}/reset-password",
            async (
                string username,
                ResetPlayerPasswordEndpoint endpoint,
                CancellationToken cancellationToken) =>
            {
                return await endpoint.HandleAsync(username, cancellationToken).ConfigureAwait(false);
            })
            .RequireAuthorization("Operator");
    }

    private async Task<IResult> HandleAsync(string username, CancellationToken cancellationToken) =>
        IdentityHttpResults.From(
            await playerAdministrationService.ResetPasswordAsync(
                new ResetPasswordRequest(username),
                cancellationToken).ConfigureAwait(false),
            value => Results.Ok(value));
}

internal sealed class CurrentPlayerEndpoint(IPlayerIdentityService playerIdentityService)
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(
            "/players/me",
            async (
                HttpContext context,
                CurrentPlayerEndpoint endpoint,
                CancellationToken cancellationToken) =>
            {
                return await endpoint.HandleAsync(context, cancellationToken).ConfigureAwait(false);
            })
            .RequireAuthorization("Player");
    }

    private async Task<IResult> HandleAsync(HttpContext context, CancellationToken cancellationToken)
    {
        string? accountId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return !Guid.TryParse(accountId, out Guid parsedAccountId)
            ? Results.Unauthorized()
            : IdentityHttpResults.From(
            await playerIdentityService.GetAsync(parsedAccountId, cancellationToken).ConfigureAwait(false),
            value => Results.Ok(value));
    }
}