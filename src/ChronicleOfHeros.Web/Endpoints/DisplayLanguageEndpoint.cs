using ChronicleOfHeros.Web.Services.Localization;

using Microsoft.AspNetCore.Mvc;

namespace ChronicleOfHeros.Web.Endpoints;

internal sealed class DisplayLanguageEndpoint(string[] supportedCultures)
{
    // Form binding makes the framework reject requests that fail .NET's automatic CSRF check with 400.
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapPost(
            "/display-language",
            (HttpContext context, DisplayLanguageEndpoint endpoint, [FromForm] string? locale, [FromForm] string? returnUrl) =>
                endpoint.Handle(context, locale, returnUrl));
    }

    private IResult Handle(HttpContext context, string? locale, string? returnUrl)
    {
        string? selectedCulture = supportedCultures.FirstOrDefault(culture =>
            string.Equals(culture, locale, StringComparison.OrdinalIgnoreCase));

        if (selectedCulture is not null)
        {
            context.Response.Cookies.Append(
                DisplayLanguageRequestCultureProvider.PreferenceCookieName,
                selectedCulture,
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(400),
                    HttpOnly = true,
                    MaxAge = TimeSpan.FromDays(400),
                    Path = "/",
                    SameSite = SameSiteMode.Lax,
                    Secure = true,
                });
        }

        return Results.Redirect(LocalReturnPath.GetSafe(returnUrl));
    }
}
