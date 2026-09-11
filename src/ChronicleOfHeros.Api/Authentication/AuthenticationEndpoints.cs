using System.IdentityModel.Tokens.Jwt;

using ChronicleOfHeros.Identity.Contracts;

namespace ChronicleOfHeros.Api.Authentication;

internal sealed class SignInEndpoint(IAuthenticationService authenticationService)
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/authentication/sign-in",
            async (
                SignInRequest request,
                SignInEndpoint endpoint,
                CancellationToken cancellationToken) =>
            {
                return await endpoint.HandleAsync(request, cancellationToken).ConfigureAwait(false);
            })
            .AllowAnonymous();
    }

    private async Task<IResult> HandleAsync(SignInRequest request, CancellationToken cancellationToken) =>
        IdentityHttpResults.From(
            await authenticationService.SignInAsync(request, cancellationToken).ConfigureAwait(false),
            value => Results.Ok(value));
}

internal sealed class ChangePasswordEndpoint(IAuthenticationService authenticationService)
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/authentication/change-password",
            async (
                ChangePasswordRequest request,
                HttpContext context,
                ChangePasswordEndpoint endpoint,
                CancellationToken cancellationToken) =>
            {
                return await endpoint.HandleAsync(request, context, cancellationToken).ConfigureAwait(false);
            })
            .RequireAuthorization("PasswordChange");
    }

    private async Task<IResult> HandleAsync(
        ChangePasswordRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var accountId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(accountId, out var parsedAccountId))
        {
            return Results.Unauthorized();
        }

        return IdentityHttpResults.From(
            await authenticationService.ChangePasswordAsync(parsedAccountId, request, cancellationToken).ConfigureAwait(false),
            value => Results.Ok(value));
    }
}

internal sealed class RefreshEndpoint(IAuthenticationService authenticationService)
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/authentication/refresh",
            async (
                RefreshTokenRequest request,
                RefreshEndpoint endpoint,
                CancellationToken cancellationToken) =>
            {
                return await endpoint.HandleAsync(request, cancellationToken).ConfigureAwait(false);
            })
            .AllowAnonymous();
    }

    private async Task<IResult> HandleAsync(RefreshTokenRequest request, CancellationToken cancellationToken) =>
        IdentityHttpResults.From(
            await authenticationService.RefreshAsync(request, cancellationToken).ConfigureAwait(false),
            value => Results.Ok(value));
}

internal sealed class SignOutEndpoint(IAuthenticationService authenticationService)
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/authentication/sign-out",
            async (
                RefreshTokenRequest request,
                SignOutEndpoint endpoint,
                CancellationToken cancellationToken) =>
            {
                return await endpoint.HandleAsync(request, cancellationToken).ConfigureAwait(false);
            })
            .AllowAnonymous();
    }

    private async Task<IResult> HandleAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await authenticationService.SignOutAsync(request, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
}