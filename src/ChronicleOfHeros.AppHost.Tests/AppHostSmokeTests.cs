using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ChronicleOfHeros.AppHost.Tests;

public class AppHostSmokeTests
{
    [Fact]
    public async Task Starts_all_resources_and_reports_healthy()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(TestContext.Current.CancellationToken);

        await using var app = await appHost.BuildAsync(TestContext.Current.CancellationToken);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();

        await resourceNotifications.WaitForResourceHealthyAsync("postgres", TestContext.Current.CancellationToken);
        await resourceNotifications.WaitForResourceHealthyAsync("api", TestContext.Current.CancellationToken);
        await resourceNotifications.WaitForResourceHealthyAsync("web", TestContext.Current.CancellationToken);
    }
}