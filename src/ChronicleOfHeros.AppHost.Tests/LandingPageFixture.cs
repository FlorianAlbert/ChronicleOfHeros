using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace ChronicleOfHeros.AppHost.Tests;

public sealed class LandingPageFixture : IAsyncLifetime
{
    private readonly SemaphoreSlim _pageGate = new(1, 1);
    private IAsyncDisposable? _app;
    private Func<HttpClient>? _createWebClient;

    private Uri BaseAddress { get; set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>();

        var app = await appHost.BuildAsync();
        _app = app;
        _createWebClient = () => app.CreateHttpClient("web");

        await app.StartAsync();

        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotifications.WaitForResourceHealthyAsync("web", CancellationToken.None);

        using var webClient = app.CreateHttpClient("web");
        BaseAddress = webClient.BaseAddress!;
    }

    public HttpClient CreateHttpClient()
    {
        var webClient = _createWebClient!();
        webClient.Timeout = TimeSpan.FromSeconds(90);
        return webClient;
    }

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

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        _pageGate.Dispose();
    }
}