using System.Text.RegularExpressions;

using Microsoft.Playwright;

namespace ChronicleOfHeros.AppHost.Tests;

/// <summary>
/// Browser tests for signing in and out through Keycloak.
/// </summary>
/// <param name="fixture">The application fixture with a freshly imported Keycloak realm.</param>
[Collection("AppHost integration")]
public class AuthenticationBrowserTests(LandingPageFixture fixture) : IClassFixture<LandingPageFixture>
{
    private readonly LandingPageFixture _fixture = fixture;

#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Verifies that the bootstrap Operator can sign in, is greeted by full name, and can sign out of Keycloak.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Bootstrap_operator_signs_in_sees_their_name_and_signs_out()
    {
        await _fixture.WithPublicPageAsync(async (page, baseAddress) =>
        {
            _ = await page.GotoAsync(new Uri(baseAddress, "/?from=sign-in").AbsoluteUri).ConfigureAwait(false);
            string paperColor = await page.Locator(".application-header")
                .EvaluateAsync<string>("element => getComputedStyle(element).backgroundColor").ConfigureAwait(false);
            await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync().ConfigureAwait(false);

            await AssertAuthenticationThemeAsync(page, paperColor).ConfigureAwait(false);
            await page.Locator("#username").FillAsync(_fixture.BootstrapUsername).ConfigureAwait(false);
            await page.Locator("#password").FillAsync("incorrect-password").ConfigureAwait(false);
            await page.Locator("#kc-login").ClickAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#input-error")).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#input-error"))
                .ToHaveCSSAsync("color", "rgb(185, 65, 42)").ConfigureAwait(false);
            await AssertAuthenticationThemeAsync(page, paperColor).ConfigureAwait(false);

            await page.Locator("#username").FillAsync(_fixture.BootstrapUsername).ConfigureAwait(false);
            await page.Locator("#password").FillAsync(_fixture.BootstrapPassword).ConfigureAwait(false);
            await page.Locator("#kc-login").ClickAsync().ConfigureAwait(false);

            string newPassword = Guid.NewGuid().ToString("N");
            await Assertions.Expect(page.Locator("#password-new")).ToBeVisibleAsync().ConfigureAwait(false);
            await AssertAuthenticationThemeAsync(page, paperColor).ConfigureAwait(false);
            await page.Locator("#password-new").FillAsync(newPassword).ConfigureAwait(false);
            await page.Locator("#password-confirm").FillAsync(newPassword).ConfigureAwait(false);
            await page.Locator("#password-confirm").PressAsync("Enter").ConfigureAwait(false);

            // The realm profile requires a name but leaves email optional.
            await Assertions.Expect(page.GetByLabel("First name")).ToBeVisibleAsync().ConfigureAwait(false);
            await AssertAuthenticationThemeAsync(page, paperColor).ConfigureAwait(false);
            await page.GetByLabel("First name").FillAsync("Test").ConfigureAwait(false);
            await page.GetByLabel("Last name").FillAsync("Operator").ConfigureAwait(false);
            await page.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync().ConfigureAwait(false);

            await Assertions.Expect(page).ToHaveURLAsync(new Uri(baseAddress, "/?from=sign-in").AbsoluteUri).ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("Hi, Test Operator!", new() { Exact = true })).ToBeVisibleAsync().ConfigureAwait(false);

            await page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync().ConfigureAwait(false);

            await Assertions.Expect(page).ToHaveURLAsync(baseAddress.AbsoluteUri).ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Sign in" })).ToBeVisibleAsync().ConfigureAwait(false);

            // Ending the Keycloak session means signing in again asks for credentials.
            await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#username")).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("/realms/chronicleofheroes/")).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Verifies that Keycloak confirmation and error pages inherit the same responsive theme.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Authentication_confirmation_and_error_pages_use_the_frontend_theme()
    {
        await _fixture.WithPublicPageAsync(async (page, baseAddress) =>
        {
            _ = await page.GotoAsync(baseAddress.AbsoluteUri).ConfigureAwait(false);
            string paperColor = await page.Locator(".application-header")
                .EvaluateAsync<string>("element => getComputedStyle(element).backgroundColor").ConfigureAwait(false);
            await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#username")).ToBeVisibleAsync().ConfigureAwait(false);

            UriBuilder keycloakUrl = new(page.Url)
            {
                Path = "/realms/chronicleofheroes/protocol/openid-connect/logout",
                Query = "client_id=chronicleofheroes-web",
            };
            _ = await page.GotoAsync(keycloakUrl.Uri.AbsoluteUri).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#kc-logout")).ToBeVisibleAsync().ConfigureAwait(false);
            await AssertAuthenticationThemeAsync(page, paperColor).ConfigureAwait(false);

            keycloakUrl.Path = "/realms/chronicleofheroes/protocol/openid-connect/auth";
            keycloakUrl.Query = "client_id=chronicleofheroes-web&response_type=code&redirect_uri=https%3A%2F%2Fexample.invalid%2F";
            _ = await page.GotoAsync(keycloakUrl.Uri.AbsoluteUri).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#kc-error-message")).ToBeVisibleAsync().ConfigureAwait(false);
            await AssertAuthenticationThemeAsync(page, paperColor).ConfigureAwait(false);
        });
    }

    private static async Task AssertAuthenticationThemeAsync(IPage page, string paperColor)
    {
        await Assertions.Expect(page.Locator("#kc-header-wrapper"))
            .ToHaveTextAsync("ChronicleOfHeros").ConfigureAwait(false);
        await Assertions.Expect(page.Locator("body"))
            .ToHaveCSSAsync("background-color", paperColor).ConfigureAwait(false);
        await Assertions.Expect(page.Locator("#kc-page-title"))
            .ToHaveCSSAsync("font-family", new Regex("Georgia")).ConfigureAwait(false);
        await Assertions.Expect(page.Locator(".card-pf"))
            .ToHaveCSSAsync("border-top-width", "1px").ConfigureAwait(false);

        foreach (int width in new[] { 320, 390, 1280 })
        {
            await page.SetViewportSizeAsync(width, 900).ConfigureAwait(false);
            Assert.True(await page.EvaluateAsync<bool>(
                "() => document.documentElement.scrollWidth <= window.innerWidth").ConfigureAwait(false),
                $"Authentication page overflows at a viewport width of {width}px.");
            await Assertions.Expect(page.Locator(".card-pf")).ToBeInViewportAsync().ConfigureAwait(false);
        }
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores
}
