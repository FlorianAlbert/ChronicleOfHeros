using System.IdentityModel.Tokens.Jwt;

using ChronicleOfHeros.Identity.Contracts;

namespace ChronicleOfHeros.Api.Authentication;

internal sealed class EnrollPlayerEndpoint(IPlayerAdministrationService playerAdministrationService)
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
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
        endpoints.MapPost(
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
        endpoints.MapGet(
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
        var accountId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(accountId, out var parsedAccountId))
        {
            return Results.Unauthorized();
        }

        return IdentityHttpResults.From(
            await playerIdentityService.GetAsync(parsedAccountId, cancellationToken).ConfigureAwait(false),
            value => Results.Ok(value));
    }
}