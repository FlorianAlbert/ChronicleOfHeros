using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace ChronicleOfHeros.Web.Services.Localization;

public sealed class DisplayLanguageRequestCultureProvider(IEnumerable<string> supportedCultureNames)
    : RequestCultureProvider
{
    public const string PreferenceCookieName = "ChronicleOfHeros.DisplayLanguage";

    private readonly CultureInfo[] _supportedCultures = supportedCultureNames
        .Select(CultureInfo.GetCultureInfo)
        .ToArray();

    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var preferredCulture = FindSupportedConcreteCulture(httpContext.Request.Cookies[PreferenceCookieName]);
        if (preferredCulture is not null)
        {
            return Task.FromResult<ProviderCultureResult?>(new(preferredCulture.Name));
        }

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

            var supportedCulture = FindSupportedCulture(requestedCulture);

            if (supportedCulture is not null)
            {
                return Task.FromResult<ProviderCultureResult?>(new(supportedCulture.Name));
            }
        }

        return Task.FromResult<ProviderCultureResult?>(null);
    }

    private CultureInfo? FindSupportedConcreteCulture(string? cultureName) =>
        string.IsNullOrWhiteSpace(cultureName)
            ? null
            : _supportedCultures.FirstOrDefault(culture =>
                string.Equals(culture.Name, cultureName, StringComparison.OrdinalIgnoreCase));

    private CultureInfo? FindSupportedCulture(CultureInfo requestedCulture)
    {
        var exactCulture = _supportedCultures.FirstOrDefault(culture =>
            string.Equals(culture.Name, requestedCulture.Name, StringComparison.OrdinalIgnoreCase));

        return exactCulture ?? _supportedCultures.FirstOrDefault(culture =>
            string.Equals(
                culture.TwoLetterISOLanguageName,
                requestedCulture.TwoLetterISOLanguageName,
                StringComparison.OrdinalIgnoreCase));
    }
}