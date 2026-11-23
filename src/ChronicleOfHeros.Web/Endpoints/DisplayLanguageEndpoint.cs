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

        return Results.Redirect(GetSafeLocalReturnPath(returnUrl));
    }

    private static string GetSafeLocalReturnPath(string? returnUrl) =>
        IsSafeLocalReturnPath(returnUrl) ? returnUrl! : "/";

    private static bool IsSafeLocalReturnPath(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)
            || returnUrl.Contains('\\', StringComparison.Ordinal)
            || !Uri.TryCreate(returnUrl, UriKind.Relative, out _))
        {
            return false;
        }

        int pathEnd = returnUrl.IndexOfAny(['?', '#']);
        string encodedPath = pathEnd < 0 ? returnUrl : returnUrl[..pathEnd];
        string decodedPath = encodedPath;

        while (true)
        {
            string nextPath = Uri.UnescapeDataString(decodedPath);
            if (nextPath == decodedPath)
            {
                break;
            }

            decodedPath = nextPath;
        }

        return decodedPath[0] == '/'
               && !decodedPath.StartsWith("//", StringComparison.Ordinal)
               && !decodedPath.StartsWith("/\\", StringComparison.Ordinal)
               && !decodedPath.Contains('\\', StringComparison.Ordinal);
    }
}
