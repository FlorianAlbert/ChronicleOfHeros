using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace ChronicleOfHeros.AppHost.Tests;

public class LandingPageBrowserTests
{
    [Fact]
    public async Task Public_root_presents_the_field_notes_landing_core_in_a_browser()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(TestContext.Current.CancellationToken);

        await using var app = await appHost.BuildAsync(TestContext.Current.CancellationToken);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();

        await resourceNotifications.WaitForResourceHealthyAsync("web", TestContext.Current.CancellationToken);

        using var webClient = app.CreateHttpClient("web");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();

        await page.GotoAsync(webClient.BaseAddress!.AbsoluteUri);

        await Assertions.Expect(page).ToHaveTitleAsync("ChronicleOfHeros");
        await Assertions.Expect(page.Locator("link[rel='icon']")).ToHaveAttributeAsync("href", "favicon.svg");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "An accurate character sheet, ready at the table." })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Armor", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Initiative", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Speed", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Coming soon" })).ToBeDisabledAsync();
        await Assertions.Expect(page.GetByLabel("Prototype variant selector")).ToHaveCountAsync(0);
    }
}