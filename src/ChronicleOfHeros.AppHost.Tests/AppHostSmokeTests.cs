using Aspire.Hosting;
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
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();

        await resourceNotifications.WaitForResourceHealthyAsync("postgres");
        await resourceNotifications.WaitForResourceHealthyAsync("Api");
        await resourceNotifications.WaitForResourceHealthyAsync("Web");
    }
}