using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChronicleOfHeros.Web.Client.Services.Localization;

public sealed class MissingTranslationDiagnosticStringLocalizerFactory(
    IOptions<LocalizationOptions> localizationOptions,
    ILoggerFactory loggerFactory) : IStringLocalizerFactory
{
    private readonly ResourceManagerStringLocalizerFactory _innerFactory = new(localizationOptions, loggerFactory);

    public IStringLocalizer Create(Type resourceSource) =>
        CreateLocalizer(_innerFactory.Create(resourceSource), resourceSource.FullName ?? resourceSource.Name);

    public IStringLocalizer Create(string baseName, string location) =>
        CreateLocalizer(_innerFactory.Create(baseName, location), baseName);

    private IStringLocalizer CreateLocalizer(IStringLocalizer innerLocalizer, string resourceName) =>
        new MissingTranslationDiagnosticStringLocalizer(
            innerLocalizer,
            loggerFactory.CreateLogger($"{nameof(MissingTranslationDiagnosticStringLocalizerFactory)}.{resourceName}"));
}

public sealed class MissingTranslationDiagnosticStringLocalizer(
    IStringLocalizer innerLocalizer,
    ILogger logger) : IStringLocalizer
{
    public LocalizedString this[string name] => RecordMissingKey(innerLocalizer[name]);

    public LocalizedString this[string name, params object[] arguments] =>
        RecordMissingKey(innerLocalizer[name, arguments]);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        innerLocalizer.GetAllStrings(includeParentCultures).Select(RecordMissingKey).ToArray();

    private LocalizedString RecordMissingKey(LocalizedString localizedString)
    {
        if (localizedString.ResourceNotFound)
        {
            logger.LogWarning(
                "Translation key {TranslationKey} is missing from its canonical English resource.",
                localizedString.Name);
        }

        return localizedString;
    }
}