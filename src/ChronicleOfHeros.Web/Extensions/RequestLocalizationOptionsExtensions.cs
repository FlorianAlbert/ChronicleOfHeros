using ChronicleOfHeros.Web.Services.Localization;

namespace ChronicleOfHeros.Web.Extensions;

internal static class RequestLocalizationOptionsExtensions
{
    extension(RequestLocalizationOptions options)
    {
        internal void ConfigureDisplayLanguages(string[] supportedCultures)
        {
            options.SetDefaultCulture("en-US")
                .AddSupportedCultures(supportedCultures)
                .AddSupportedUICultures(supportedCultures);
            options.RequestCultureProviders =
            [
                new DisplayLanguageRequestCultureProvider(supportedCultures),
            ];
            options.ApplyCurrentCultureToResponseHeaders = true;
        }
    }
}