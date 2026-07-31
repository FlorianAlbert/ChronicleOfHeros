using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace ChronicleOfHeros.Web.Services.Localization;

public sealed class DisplayLanguageRequestCultureProvider(IEnumerable<string> supportedCultureNames)
    : RequestCultureProvider
{
    private readonly CultureInfo[] _supportedCultures = supportedCultureNames
        .Select(CultureInfo.GetCultureInfo)
        .ToArray();

    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var browserLanguages = httpContext.Request.GetTypedHeaders().AcceptLanguage;
        if (browserLanguages is null)
        {
            return Task.FromResult<ProviderCultureResult?>(null);
        }

        foreach (var browserLanguage in browserLanguages.OrderByDescending(language => language.Quality ?? 1))
        {
            if (browserLanguage.Quality == 0 || browserLanguage.Value == "*")
            {
                continue;
            }

            CultureInfo requestedCulture;
            try
            {
                requestedCulture = CultureInfo.GetCultureInfo(browserLanguage.Value.Value!);
            }
            catch (CultureNotFoundException)
            {
                continue;
            }

            var supportedCulture = _supportedCultures.FirstOrDefault(culture =>
                string.Equals(culture.Name, requestedCulture.Name, StringComparison.OrdinalIgnoreCase));

            supportedCulture ??= _supportedCultures.FirstOrDefault(culture =>
                string.Equals(
                    culture.TwoLetterISOLanguageName,
                    requestedCulture.TwoLetterISOLanguageName,
                    StringComparison.OrdinalIgnoreCase));

            if (supportedCulture is not null)
            {
                return Task.FromResult<ProviderCultureResult?>(new(supportedCulture.Name));
            }
        }

        return Task.FromResult<ProviderCultureResult?>(null);
    }
}