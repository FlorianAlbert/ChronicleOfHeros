using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

namespace ChronicleOfHeros.Web.Endpoints;

internal static class AuthenticationEndpoint
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/authentication")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());

        _ = group.MapPost("/login", ([FromForm] string? returnUrl) => Login(returnUrl));
        _ = group.MapPost("/logout", Logout);
    }

    private static IResult Login(string? returnUrl) =>
        Results.Challenge(
            new AuthenticationProperties { RedirectUri = LocalReturnPath.GetSafe(returnUrl) },
            [OpenIdConnectDefaults.AuthenticationScheme]);

    private static IResult Logout() =>
        Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
}
