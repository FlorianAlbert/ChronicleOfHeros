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
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(baseAddress.AbsoluteUri);

            await Assertions.Expect(page).ToHaveTitleAsync("ChronicleOfHeros");
            await Assertions.Expect(page.Locator("link[rel='icon']")).ToHaveAttributeAsync("href", "favicon.svg");
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "An accurate character sheet, ready at the table." })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Armor", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Initiative", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Speed", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Coming soon" })).ToBeDisabledAsync();
            await Assertions.Expect(page.GetByLabel("Prototype variant selector")).ToHaveCountAsync(0);
        });
    }

    [Fact]
    public async Task Public_root_explains_the_character_management_journey_through_on_page_navigation()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(baseAddress.AbsoluteUri);

            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Character Sheets" })).ToHaveAttributeAsync("href", "#character-sheet");
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "How It Works" })).ToHaveAttributeAsync("href", "#how-it-works");
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "About" })).ToHaveAttributeAsync("href", "#about");
            await Assertions.Expect(page.Locator("#how-it-works article").Nth(0)).ToContainTextAsync("choices");
            await Assertions.Expect(page.Locator("#how-it-works article").Nth(1)).ToContainTextAsync("derived values");
            await Assertions.Expect(page.Locator("#how-it-works article").Nth(2)).ToContainTextAsync("level");
            await Assertions.Expect(page.Locator("#about").GetByRole(AriaRole.Heading, new() { Name = "About ChronicleOfHeros" })).ToBeVisibleAsync();
        });
    }

    [Fact]
    public async Task Invalid_url_presents_a_branded_way_back_to_the_public_root()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(new Uri(baseAddress, "/a-page-that-does-not-exist").AbsoluteUri);

            await Assertions.Expect(page).ToHaveTitleAsync("Page not found | ChronicleOfHeros");
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "ChronicleOfHeros" })).ToHaveAttributeAsync("href", "/");
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "This page is missing from the record." })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Return to the character sheet" })).ToHaveAttributeAsync("href", "/");
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Not Found", Exact = true })).ToHaveCountAsync(0);
        });
    }

    private static async Task WithPublicPageAsync(Func<IPage, Uri, Task> exercisePage)
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

        await exercisePage(page, webClient.BaseAddress!);
    }
}