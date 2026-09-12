using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace ChronicleOfHeros.AppHost.Tests;

// xUnit requires public types for fixtures.
#pragma warning disable CA1515 // Consider making public types internal

/// <summary>
/// Fixture for testing the landing page of the Chronicle of Heroes application. This fixture sets up an application host, provides methods to create HTTP clients, and allows for interaction with public pages using Playwright. It ensures that resources are properly initialized and disposed of during testing.
/// </summary>
public sealed class LandingPageFixture : IAsyncLifetime
{
    private const string NoRedirectWebClientName = "LandingPageNoRedirectWeb";

    private readonly SemaphoreSlim _pageGate = new(1, 1);
    private DistributedApplication? _app;
    private Func<HttpClient>? _createNoRedirectWebClient;
    private Func<HttpClient>? _createWebClient;

    private Uri BaseAddress { get; set; } = null!;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        IDistributedApplicationTestingBuilder appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(
                BootstrapOperatorTestParameters.CreateAppHostArguments()).ConfigureAwait(false);
        _ = appHost.Services.AddHttpClient(NoRedirectWebClientName)
            .ConfigurePrimaryHttpMessageHandler(static () => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                CheckCertificateRevocationList = true,
                UseCookies = false,
            });

        DistributedApplication app = await appHost.BuildAsync().ConfigureAwait(false);
        _app = app;
        _createWebClient = () => app.CreateHttpClient("web");
        IHttpClientFactory httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
        _createNoRedirectWebClient = () =>
        {
            HttpClient webClient = httpClientFactory.CreateClient(NoRedirectWebClientName);
            webClient.BaseAddress = BaseAddress;
            return webClient;
        };

        await app.StartAsync().ConfigureAwait(false);

        ResourceNotificationService resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();
        _ = await resourceNotifications.WaitForResourceHealthyAsync("web", CancellationToken.None).ConfigureAwait(false);

        using HttpClient webClient = app.CreateHttpClient("web");
        BaseAddress = webClient.BaseAddress!;
    }

    /// <summary>
    /// Creates an HttpClient for interacting with the application. The client can be configured to allow or disallow automatic redirection of HTTP requests.
    /// </summary>
    /// <param name="allowAutoRedirect">Indicates whether the HttpClient should automatically follow HTTP redirects.</param>
    /// <returns>An HttpClient instance configured according to the specified parameters.</returns>
    public HttpClient CreateHttpClient(bool allowAutoRedirect = true)
    {
        HttpClient webClient = allowAutoRedirect
            ? _createWebClient!()
            : _createNoRedirectWebClient!();
        webClient.Timeout = TimeSpan.FromSeconds(90);

        return webClient;
    }

    /// <summary>
    /// Executes a provided asynchronous function that interacts with a public page of the application using Playwright. This method ensures that only one page interaction occurs at a time by using a semaphore for synchronization. It allows for configuration of JavaScript execution and locale settings for the browser context.
    /// </summary>
    /// <param name="exercisePage">The asynchronous function to execute, which interacts with the public page.</param>
    /// <param name="javaScriptEnabled">Indicates whether JavaScript should be enabled in the browser context.</param>
    /// <param name="locale">The locale to use for the browser context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task WithPublicPageAsync(
        Func<IPage, Uri, Task> exercisePage,
        bool javaScriptEnabled = true,
        string? locale = null)
    {
        await _pageGate.WaitAsync().ConfigureAwait(false);

        try
        {
            using IPlaywright playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            IBrowser browser = await playwright.Chromium.LaunchAsync().ConfigureAwait(false);
            await using (browser.ConfigureAwait(false))
            {
                IBrowserContext browserContext = await browser.NewContextAsync(new()
                {
                    JavaScriptEnabled = javaScriptEnabled,
                    Locale = locale,
                    ReducedMotion = ReducedMotion.NoPreference,
                }).ConfigureAwait(false);
                await using (browserContext.ConfigureAwait(false))
                {
                    IPage page = await browserContext.NewPageAsync().ConfigureAwait(false);
                    Assert.NotNull(exercisePage);
                    await exercisePage(page, BaseAddress).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _ = _pageGate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync().ConfigureAwait(false);
        }

        _pageGate.Dispose();
    }
}

#pragma warning restore CA1515 // Consider making public types internal