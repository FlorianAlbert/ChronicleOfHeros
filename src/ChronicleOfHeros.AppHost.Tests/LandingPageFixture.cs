using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace ChronicleOfHeros.AppHost.Tests;

/// <summary>
/// Fixture for testing the landing page of the Chronicle of Heroes application. This fixture sets up an application host, provides methods to create HTTP clients, and allows for interaction with public pages using Playwright. It ensures that resources are properly initialized and disposed of during testing.
/// </summary>
public sealed class LandingPageFixture : IAsyncLifetime
{
    private readonly SemaphoreSlim _pageGate = new(1, 1);
    private IAsyncDisposable? _app;
    private Func<HttpClient>? _createWebClient;

    private Uri BaseAddress { get; set; } = null!;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(
                BootstrapOperatorTestParameters.CreateAppHostArguments());

        var app = await appHost.BuildAsync();
        _app = app;
        _createWebClient = () => app.CreateHttpClient("web");

        await app.StartAsync();

        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotifications.WaitForResourceHealthyAsync("web", CancellationToken.None);

        using var webClient = app.CreateHttpClient("web");
        BaseAddress = webClient.BaseAddress!;
    }
    
    /// <summary>
    /// Creates an HttpClient for interacting with the application. The client can be configured to allow or disallow automatic redirection of HTTP requests.
    /// </summary>
    /// <param name="allowAutoRedirect">Indicates whether the HttpClient should automatically follow HTTP redirects.</param>
    /// <returns>An HttpClient instance configured according to the specified parameters.</returns>
    public HttpClient CreateHttpClient(bool allowAutoRedirect = true)
    {
        if (!allowAutoRedirect)
        {
            return new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            })
            {
                BaseAddress = BaseAddress,
                Timeout = TimeSpan.FromSeconds(90),
            };
        }

        var webClient = _createWebClient!();
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
        await _pageGate.WaitAsync();

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync();
            await using var context = await browser.NewContextAsync(new()
            {
                JavaScriptEnabled = javaScriptEnabled,
                Locale = locale,
                ReducedMotion = ReducedMotion.NoPreference,
            });
            var page = await context.NewPageAsync();
            await exercisePage(page, BaseAddress);
        }
        finally
        {
            _pageGate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        _pageGate.Dispose();
    }
}