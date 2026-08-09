using System.Globalization;
using System.Xml.Linq;
using ChronicleOfHeros.Web.Client.Services.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChronicleOfHeros.AppHost.Tests;

public sealed class TranslationResourceTests
{
    private static readonly string[] SupportedDisplayLanguages = ["de-DE"];

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
        XDocument.Load(resourcePath)
            .Root!
            .Elements("data")
            .Select(element => element.Attribute("name")!.Value)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

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

public sealed class TranslationFallbackProbe;