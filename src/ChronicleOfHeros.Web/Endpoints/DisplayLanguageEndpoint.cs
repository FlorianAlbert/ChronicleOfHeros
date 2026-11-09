using ChronicleOfHeros.Web.Services.Localization;

using Microsoft.AspNetCore.Antiforgery;

namespace ChronicleOfHeros.Web.Endpoints;

internal sealed class DisplayLanguageEndpoint(IAntiforgery antiforgery, string[] supportedCultures)
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapPost(
            "/display-language",
            async (HttpContext context, DisplayLanguageEndpoint endpoint) =>
            {
                return await endpoint.HandleAsync(context).ConfigureAwait(false);
            });
    }

    private async Task<IResult> HandleAsync(HttpContext context)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest();
        }

        IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
        string? selectedCulture = supportedCultures.FirstOrDefault(culture =>
            string.Equals(culture, form["locale"], StringComparison.OrdinalIgnoreCase));

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

        return Results.Redirect(GetSafeLocalReturnPath(form["returnUrl"].ToString()));
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
