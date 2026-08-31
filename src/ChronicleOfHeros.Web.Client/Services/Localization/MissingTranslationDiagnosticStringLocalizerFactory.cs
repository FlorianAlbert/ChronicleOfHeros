using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace ChronicleOfHeros.Web.Client.Services.Localization;

/// <summary>
/// A factory for creating <see cref="MissingTranslationDiagnosticStringLocalizer"/> instances that wrap the default <see cref="ResourceManagerStringLocalizerFactory"/>.
/// </summary>
/// <param name="localizationOptions">The localization options.</param>
/// <param name="loggerFactory">The logger factory.</param>
public sealed class MissingTranslationDiagnosticStringLocalizerFactory(
    IOptions<LocalizationOptions> localizationOptions,
    ILoggerFactory loggerFactory) : IStringLocalizerFactory
{
    private readonly ResourceManagerStringLocalizerFactory _innerFactory = new(localizationOptions, loggerFactory);

    /// <inheritdoc />
    public IStringLocalizer Create(Type resourceSource) =>
        CreateLocalizer(_innerFactory.Create(resourceSource), resourceSource?.FullName ?? resourceSource?.Name ?? throw new ArgumentNullException(nameof(resourceSource)));

    /// <inheritdoc/>
    public IStringLocalizer Create(string baseName, string location) =>
        CreateLocalizer(_innerFactory.Create(baseName, location), baseName);

    private MissingTranslationDiagnosticStringLocalizer CreateLocalizer(IStringLocalizer innerLocalizer, string resourceName) =>
        new MissingTranslationDiagnosticStringLocalizer(
            innerLocalizer,
            loggerFactory.CreateLogger($"{nameof(MissingTranslationDiagnosticStringLocalizerFactory)}.{resourceName}"));
}

internal sealed partial class MissingTranslationDiagnosticStringLocalizer(
    IStringLocalizer innerLocalizer,
    ILogger logger) : IStringLocalizer
{
    public LocalizedString this[string name] => RecordMissingKey(innerLocalizer[name]);

    public LocalizedString this[string name, params object[] arguments] =>
        RecordMissingKey(innerLocalizer[name, arguments]);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        [.. innerLocalizer.GetAllStrings(includeParentCultures).Select(RecordMissingKey)];

    private LocalizedString RecordMissingKey(LocalizedString localizedString)
    {
        if (localizedString.ResourceNotFound)
        {
            LogMissingKey(localizedString.Name);
        }

        return localizedString;
    }

    [LoggerMessage(LogLevel.Warning, "Translation key {TranslationKey} is missing from its canonical English resource.")]
    private partial void LogMissingKey(string translationKey);
}