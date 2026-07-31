using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace ChronicleOfHeros.AppHost.Tests;

public class AppHostSmokeTests
{
    private static readonly TimeSpan HealthRequestTimeout = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task Health_endpoints_are_available_through_the_web_host()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(TestContext.Current.CancellationToken);

        await using var app = await appHost.BuildAsync(TestContext.Current.CancellationToken);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();

        await resourceNotifications.WaitForResourceHealthyAsync("postgres", TestContext.Current.CancellationToken);
        await resourceNotifications.WaitForResourceHealthyAsync("api", TestContext.Current.CancellationToken);
        await resourceNotifications.WaitForResourceHealthyAsync("web", TestContext.Current.CancellationToken);

        using var webClient = app.CreateHttpClient("web");
        webClient.Timeout = HealthRequestTimeout;

        var webHealthResponse = await webClient.GetAsync("/health", TestContext.Current.CancellationToken);
        var apiHealthResponse = await webClient.GetAsync("/api/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, webHealthResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, apiHealthResponse.StatusCode);
    }
}