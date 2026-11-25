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
            await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync().ConfigureAwait(false);

            await page.Locator("#username").FillAsync(_fixture.BootstrapUsername).ConfigureAwait(false);
            await page.Locator("#password").FillAsync(_fixture.BootstrapPassword).ConfigureAwait(false);
            await page.Locator("#kc-login").ClickAsync().ConfigureAwait(false);

            string newPassword = Guid.NewGuid().ToString("N");
            await page.Locator("#password-new").FillAsync(newPassword).ConfigureAwait(false);
            await page.Locator("#password-confirm").FillAsync(newPassword).ConfigureAwait(false);
            await page.Locator("#password-confirm").PressAsync("Enter").ConfigureAwait(false);

            // The realm profile requires a name but leaves email optional.
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

#pragma warning restore CA1707 // Identifiers should not contain underscores
}
