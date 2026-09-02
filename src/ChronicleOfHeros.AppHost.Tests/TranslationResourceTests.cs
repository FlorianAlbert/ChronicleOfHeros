using System.Globalization;
using System.Xml.Linq;

using ChronicleOfHeros.Web.Client.Services.Localization;

using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChronicleOfHeros.AppHost.Tests;

/// <summary>
/// Tests for verifying the correctness and completeness of translation resources in the Chronicle of Heroes application. These tests ensure that missing translations fall back to canonical English values, that missing canonical English keys are logged appropriately, and that production translation resources match their corresponding canonical English keys for supported display languages.
/// </summary>
public sealed class TranslationResourceTests
{
    private static readonly string[] SupportedDisplayLanguages = ["de-DE"];

    /// <summary>
    /// Verifies that when a requested translation key is missing for a specific language, the application falls back to the canonical English value. This test sets the current culture to German (de-DE) and checks that the missing translation key returns the expected English fallback value without indicating a resource not found error.
    /// </summary>
    [Fact]
    public void Missing_requested_language_key_uses_the_canonical_English_value()
    {
        using var cultureScope = new CultureScope("de-DE");
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var localizerFactory = new MissingTranslationDiagnosticStringLocalizerFactory(
            Options.Create(new LocalizationOptions()),
            loggerFactory);

        var value = localizerFactory.Create(typeof(TranslationFallbackProbe))["EnglishFallback"];

        Assert.Equal("English fallback", value.Value);
        Assert.False(value.ResourceNotFound);
    }

    /// <summary>
    /// Verifies that when a canonical English translation key is missing, the application logs a warning diagnostic and returns the key itself as the value. This test sets the current culture to English (en-US) and checks that the missing canonical English key returns the key name, indicates a resource not found error, and logs an appropriate warning message.
    /// </summary>
    [Fact]
    public void Missing_canonical_English_key_displays_the_key_and_records_a_diagnostic()
    {
        using var cultureScope = new CultureScope("en-US");
        var loggerProvider = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
        var localizerFactory = new MissingTranslationDiagnosticStringLocalizerFactory(
            Options.Create(new LocalizationOptions()),
            loggerFactory);

        var value = localizerFactory.Create(typeof(TranslationFallbackProbe))["AbsentEnglish"];

        Assert.Equal("AbsentEnglish", value.Value);
        Assert.True(value.ResourceNotFound);
        var diagnostic = Assert.Single(loggerProvider.Entries);
        Assert.Equal(LogLevel.Warning, diagnostic.LogLevel);
        Assert.Contains("Translation key AbsentEnglish is missing from its canonical English resource.", diagnostic.Message);
    }

    /// <summary>
    /// Verifies that all production translation resources for supported display languages match their corresponding canonical English keys. This test iterates through the canonical English resource files and checks that each supported display language has a corresponding resource file with the same set of keys. If any discrepancies are found, the test fails, indicating that the translation resources are not in sync with the canonical English keys.
    /// </summary>
    [Fact]
    public void Production_translation_resources_match_their_canonical_English_keys()
    {
        var repositoryRoot = FindRepositoryRoot();
        var canonicalResources = new[]
            {
                Path.Combine(repositoryRoot, "src", "ChronicleOfHeros.Web"),
                Path.Combine(repositoryRoot, "src", "ChronicleOfHeros.Web.Client"),
            }
            .SelectMany(projectDirectory => Directory.EnumerateFiles(projectDirectory, "*.resx", SearchOption.AllDirectories))
            .Where(path => !Path.GetFileNameWithoutExtension(path).EndsWith(".de-DE", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);

        foreach (var canonicalResource in canonicalResources)
        {
            var canonicalKeys = GetResourceKeys(canonicalResource);

            foreach (var displayLanguage in SupportedDisplayLanguages)
            {
                var localizedResource = Path.ChangeExtension(canonicalResource, $"{displayLanguage}.resx");
                Assert.True(
                    File.Exists(localizedResource),
                    $"{localizedResource} is required for the supported {displayLanguage} display language.");
                Assert.Equal(canonicalKeys, GetResourceKeys(localizedResource));
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ChronicleOfHeros.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the ChronicleOfHeros repository root.");
    }

    private static string[] GetResourceKeys(string resourcePath) =>
        [.. XDocument.Load(resourcePath)
            .Root!
            .Elements("data")
            .Select(element => element.Attribute("name")!.Value)
            .OrderBy(name => name, StringComparer.Ordinal)];

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _originalUICulture = CultureInfo.CurrentUICulture;

        public CultureScope(string cultureName)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUICulture;
        }
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<LogEntry> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(Entries);

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger(List<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Add(new(logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message);
}

/// <summary>
/// A probe class used for testing translation resource behavior. This class serves as a placeholder for localization tests, allowing the test framework to verify the handling of translation keys and fallbacks without relying on actual application logic. It is used to simulate scenarios where translation keys may be missing or require fallback to canonical English values.
/// </summary>
internal sealed class TranslationFallbackProbe;