using System.IdentityModel.Tokens.Jwt;

using ChronicleOfHeros.Identity.Contracts;

namespace ChronicleOfHeros.Api.Authentication;

internal static class AuthenticationEndpointsExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddAuthenticationEndpoints()
        {
            _ = builder.Services.AddScoped(services => new SignInEndpoint(
                services.GetRequiredService<IAuthenticationService>()));
            _ = builder.Services.AddScoped(services => new ChangePasswordEndpoint(
                services.GetRequiredService<IAuthenticationService>()));
            _ = builder.Services.AddScoped(services => new RefreshEndpoint(
                services.GetRequiredService<IAuthenticationService>()));
            _ = builder.Services.AddScoped(services => new SignOutEndpoint(
                services.GetRequiredService<IAuthenticationService>()));

            return builder;
        }
    }

    extension(WebApplication webApplication)
    {
        public WebApplication MapAuthenticationEndpoints()
        {
            SignInEndpoint.Map(webApplication);
            ChangePasswordEndpoint.Map(webApplication);
            RefreshEndpoint.Map(webApplication);
            SignOutEndpoint.Map(webApplication);
            return webApplication;
        }
    }
}

internal sealed class SignInEndpoint(IAuthenticationService authenticationService)
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapPost(
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
        _ = endpoints.MapPost(
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
        string? accountId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return !Guid.TryParse(accountId, out Guid parsedAccountId)
            ? Results.Unauthorized()
            : IdentityHttpResults.From(
            await authenticationService.ChangePasswordAsync(parsedAccountId, request, cancellationToken).ConfigureAwait(false),
            value => Results.Ok(value));
    }
}

internal sealed class RefreshEndpoint(IAuthenticationService authenticationService)
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapPost(
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
        _ = endpoints.MapPost(
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