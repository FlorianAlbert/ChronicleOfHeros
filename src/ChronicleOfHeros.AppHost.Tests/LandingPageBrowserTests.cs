using System.Net;
using System.Text.RegularExpressions;

using Microsoft.Playwright;

namespace ChronicleOfHeros.AppHost.Tests;

/// <summary>
/// Tests for the landing page experience in a browser, including localization, accessibility, and responsive design.
/// </summary>
[Collection("AppHost integration")]
public class LandingPageBrowserTests : IClassFixture<LandingPageFixture>
{
    private readonly LandingPageFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="LandingPageBrowserTests"/> class with the specified fixture.
    /// </summary>
    /// <param name="fixture">The landing page fixture.</param>
    public LandingPageBrowserTests(LandingPageFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Tests that the public root page presents the field notes landing core in a browser, verifying the title, favicon, headings, and other elements for correctness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Public_root_presents_the_field_notes_landing_core_in_a_browser()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(baseAddress.AbsoluteUri).ConfigureAwait(false);

            await Assertions.Expect(page).ToHaveTitleAsync("ChronicleOfHeros | Your character sheet at the table").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("link[rel='icon']")).ToHaveAttributeAsync("href", "favicon.svg").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "An accurate character sheet, ready at the table." })).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("Armor", new() { Exact = true })).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("Initiative", new() { Exact = true })).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("Speed", new() { Exact = true })).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Coming soon" })).ToBeDisabledAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByLabel("Prototype variant selector")).ToHaveCountAsync(0).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Tests that the public root page renders in German when the browser's language preference is set to German or related locales, verifying the title, headings, and other elements for correctness.
    /// </summary>
    /// <param name="browserLanguage">The browser's language preference.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Theory]
    [InlineData("de")]
    [InlineData("de-AT")]
    [InlineData("de-CH")]
    [InlineData("de-DE")]
    [InlineData("fr-FR, de-CH;q=0.9, en-US;q=0.8")]
    public async Task Public_root_renders_German_for_a_German_browser_preference(string browserLanguage)
    {
        using var webClient = _fixture.CreateHttpClient();
        webClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd(browserLanguage);

        using var response = await webClient.GetAsync("/", TestContext.Current.CancellationToken);
        var landingPage = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var decodedLandingPage = WebUtility.HtmlDecode(landingPage);

        Assert.Equal(["de-DE"], response.Content.Headers.ContentLanguage);
        Assert.Contains("<html lang=\"de\">", landingPage);
        Assert.Contains("<title>ChronicleOfHeros | Dein Charakterbogen am Spieltisch</title>", landingPage);
        Assert.Contains("aria-label=\"Hauptnavigation\"", landingPage);
        Assert.Contains("Ein präziser Charakterbogen, bereit für den Spieltisch.", decodedLandingPage);
        Assert.Contains(">Rüstungsklasse<", decodedLandingPage);
        Assert.Contains(">30 ft.<", decodedLandingPage);
        Assert.DoesNotContain("An accurate character sheet, ready at the table.", decodedLandingPage);
    }

    /// <summary>
    /// Tests that the public root page defaults to English when the browser's language preference is set to English or unsupported locales, verifying the title, headings, and other elements for correctness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Public_root_uses_English_for_English_and_unsupported_browser_preferences()
    {
        using var webClient = _fixture.CreateHttpClient();

        foreach (var browserLanguage in new string?[] { null, "en", "en-US", "en-GB", "fr-FR" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/");
            if (browserLanguage is not null)
            {
                request.Headers.AcceptLanguage.ParseAdd(browserLanguage);
            }

            using var response = await webClient.SendAsync(request, TestContext.Current.CancellationToken);
            var landingPage = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal(["en-US"], response.Content.Headers.ContentLanguage);
            Assert.Contains("<html lang=\"en\">", landingPage);
            Assert.Contains("<title>ChronicleOfHeros | Your character sheet at the table</title>", landingPage);
            Assert.Contains("aria-label=\"Primary navigation\"", landingPage);
            Assert.Contains("An accurate character sheet, ready at the table.", landingPage);
            Assert.Contains(">Armor<", landingPage);
            Assert.DoesNotContain("Ein präziser Charakterbogen, bereit für den Spieltisch.", landingPage);
        }
    }

    /// <summary>
    /// Tests that the reconnect dialog displays the appropriate rejoining feedback message in the active display language when the browser's language preference is set to English or German.
    /// </summary>
    /// <param name="browserLanguage">The browser's language preference.</param>
    /// <param name="expectedRejoining">The expected rejoining feedback message.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Theory]
    [InlineData("en-US", "Rejoining the server...")]
    [InlineData("de-DE", "Verbindung mit dem Server wird wiederhergestellt...")]
    public async Task Reconnect_dialog_displays_rejoining_feedback_in_the_active_display_language(
        string browserLanguage,
        string expectedRejoining)
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(baseAddress.AbsoluteUri).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);

            await page.EvaluateAsync(
                """
                () => {
                    const reconnectModal = document.getElementById("components-reconnect-modal");
                    reconnectModal.classList.add("components-reconnect-show");
                    reconnectModal.dispatchEvent(new CustomEvent("components-reconnect-state-changed", { detail: { state: "show" } }));
                }
                """).ConfigureAwait(false);

            var reconnectDialog = page.Locator("#components-reconnect-modal");
            await Assertions.Expect(reconnectDialog).ToHaveAttributeAsync("open", string.Empty).ConfigureAwait(false);
            await Assertions.Expect(reconnectDialog.GetByText(expectedRejoining, new() { Exact = true })).ToBeVisibleAsync().ConfigureAwait(false);
        }, locale: browserLanguage);
    }

    /// <summary>
    /// Tests that the reachable error boundary renders feedback and recovery actions in the active display language based on the browser's language preference, verifying the title, feedback message, recovery action, and display language label for correctness.
    /// </summary>
    /// <param name="browserLanguage">The browser's language preference.</param>
    /// <param name="expectedCulture">The expected culture of the error page.</param>
    /// <param name="expectedTitle">The expected title of the error page.</param>
    /// <param name="expectedFeedback">The expected feedback message displayed on the error page.</param>
    /// <param name="expectedRecoveryAction">The expected recovery action text displayed on the error page.</param>
    /// <param name="expectedDisplayLanguageLabel">The expected label for the display language selector.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Theory]
    [InlineData("en-US", "en-US", "Something went wrong | ChronicleOfHeros", "We could not complete that request.", "Return to the character sheet", "Display language")]
    [InlineData("de-DE", "de-DE", "Etwas ist schiefgelaufen | ChronicleOfHeros", "Diese Anfrage konnte nicht abgeschlossen werden.", "Zurück zum Charakterbogen", "Anzeigesprache")]
    public async Task Reachable_error_boundary_renders_feedback_and_recovery_in_the_active_display_language(
        string browserLanguage,
        string expectedCulture,
        string expectedTitle,
        string expectedFeedback,
        string expectedRecoveryAction,
        string expectedDisplayLanguageLabel)
    {
        using var webClient = _fixture.CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/error");
        request.Headers.AcceptLanguage.ParseAdd(browserLanguage);

        using var response = await webClient.SendAsync(request, TestContext.Current.CancellationToken);
        var errorPage = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var decodedErrorPage = WebUtility.HtmlDecode(errorPage);

        Assert.Equal([expectedCulture], response.Content.Headers.ContentLanguage);
        Assert.Contains($"<title>{expectedTitle}</title>", errorPage);
        Assert.Contains(expectedFeedback, decodedErrorPage);
        Assert.Contains($">{expectedRecoveryAction}<", decodedErrorPage);
        Assert.Contains($"aria-label=\"{expectedDisplayLanguageLabel}\"", errorPage);
    }

    /// <summary>
    /// Tests that an unknown local route retains a 404 status and renders the localized not found experience based on the browser's language preference, verifying the culture, document language, title, heading, return action, and display language label for correctness.
    /// </summary>
    /// <param name="browserLanguage">The browser's language preference.</param>
    /// <param name="expectedCulture">The expected culture of the not found page.</param>
    /// <param name="expectedDocumentLanguage">The expected document language of the not found page.</param>
    /// <param name="expectedTitle">The expected title of the not found page.</param>
    /// <param name="expectedHeading">The expected heading displayed on the not found page.</param>
    /// <param name="expectedReturnAction">The expected return action text displayed on the not found page.</param>
    /// <param name="expectedDisplayLanguageLabel">The expected label for the display language selector.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Theory]
    [InlineData("en-US", "en-US", "en", "Page not found | ChronicleOfHeros", "This page is missing from the record.", "Return to the character sheet", "Display language")]
    [InlineData("de-DE", "de-DE", "de", "Seite nicht gefunden | ChronicleOfHeros", "Diese Seite fehlt im Register.", "Zurück zum Charakterbogen", "Anzeigesprache")]
    public async Task Unknown_local_route_retains_404_status_and_renders_the_localized_not_found_experience(
        string browserLanguage,
        string expectedCulture,
        string expectedDocumentLanguage,
        string expectedTitle,
        string expectedHeading,
        string expectedReturnAction,
        string expectedDisplayLanguageLabel)
    {
        using var webClient = _fixture.CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/a-page-that-does-not-exist");
        request.Headers.AcceptLanguage.ParseAdd(browserLanguage);

        using var response = await webClient.SendAsync(request, TestContext.Current.CancellationToken);
        var notFoundPage = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var decodedNotFoundPage = WebUtility.HtmlDecode(notFoundPage);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal([expectedCulture], response.Content.Headers.ContentLanguage);
        Assert.Contains($"<html lang=\"{expectedDocumentLanguage}\">", notFoundPage);
        Assert.Contains($"<title>{expectedTitle}</title>", notFoundPage);
        Assert.Contains(expectedHeading, decodedNotFoundPage);
        Assert.Contains($"aria-label=\"{expectedDisplayLanguageLabel}\"", decodedNotFoundPage);
        Assert.Contains($">{expectedReturnAction}<", decodedNotFoundPage);
    }

    /// <summary>
    /// Tests that the public root page respects an explicit display language cookie, overriding the browser's language preference, and renders the page in the specified language, verifying the content language, document language, and title for correctness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Public_root_explicit_display_language_cookie_overrides_browser_preference()
    {
        using var webClient = _fixture.CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.AcceptLanguage.ParseAdd("en-US");
        request.Headers.Add("Cookie", "ChronicleOfHeros.DisplayLanguage=de-DE");

        using var response = await webClient.SendAsync(request, TestContext.Current.CancellationToken);
        var landingPage = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["de-DE"], response.Content.Headers.ContentLanguage);
        Assert.Contains("<html lang=\"de\">", landingPage);
        Assert.Contains("<title>ChronicleOfHeros | Dein Charakterbogen am Spieltisch</title>", landingPage);
    }

    /// <summary>
    /// Tests that the public root page ignores a non-concrete display language cookie, falling back to the browser's language preference, and renders the page in the appropriate language, verifying the content language, document language, and title for correctness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Public_root_ignores_a_non_concrete_display_language_cookie()
    {
        using var webClient = _fixture.CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.AcceptLanguage.ParseAdd("en-US");
        request.Headers.Add("Cookie", "ChronicleOfHeros.DisplayLanguage=de");

        using var response = await webClient.SendAsync(request, TestContext.Current.CancellationToken);
        var landingPage = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["en-US"], response.Content.Headers.ContentLanguage);
        Assert.Contains("<html lang=\"en\">", landingPage);
        Assert.Contains("<title>ChronicleOfHeros | Your character sheet at the table</title>", landingPage);
    }

    /// <summary>
    /// Tests that a supported display language choice persists a secure preference cookie and redirects back to the specified local path, verifying the response status, location header, and cookie attributes for correctness.
    /// </summary>
    /// <param name="selectedLanguage">The display language selected by the user.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    public async Task Supported_display_language_choice_persists_a_secure_preference_and_returns_to_the_local_path(string selectedLanguage)
    {
        using var webClient = _fixture.CreateHttpClient(allowAutoRedirect: false);
        var antiforgery = await GetAntiforgeryTokenAsync(webClient);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/display-language")
        {
            Content = new FormUrlEncodedContent(
            [
                new("locale", selectedLanguage),
                new("returnUrl", "/?character-sheet"),
                new("__RequestVerificationToken", antiforgery.Token),
            ]),
        };
        request.Headers.Add("Cookie", $"{antiforgery.Cookie}; ChronicleOfHeros.DisplayLanguage=en-US");

        using var response = await webClient.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/?character-sheet", response.Headers.Location?.OriginalString);

        var preferenceCookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("ChronicleOfHeros.DisplayLanguage=", StringComparison.Ordinal));

        Assert.StartsWith($"ChronicleOfHeros.DisplayLanguage={selectedLanguage};", preferenceCookie, StringComparison.Ordinal);
        Assert.Contains("max-age=34560000", preferenceCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", preferenceCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", preferenceCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", preferenceCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", preferenceCookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that a display language choice submission without an anti-forgery token is rejected with a Bad Request response, verifying the response status and absence of a Set-Cookie header for the preference cookie.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Display_language_choice_without_an_antiforgery_token_is_rejected()
    {
        using var webClient = _fixture.CreateHttpClient(allowAutoRedirect: false);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/display-language")
        {
            Content = new FormUrlEncodedContent(
            [
                new("locale", "de-DE"),
                new("returnUrl", "/"),
            ]),
        };

        using var response = await webClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
    }

    /// <summary>
    /// Tests that an unsupported display language choice does not change the existing preference cookie and redirects back to the specified local path, verifying the response status, location header, and absence of a Set-Cookie header for the preference cookie.
    /// </summary>
    /// <param name="selectedLanguage">The display language selected by the user.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Theory]
    [InlineData("de")]
    [InlineData("fr-FR")]
    [InlineData("invalid-locale")]
    public async Task Unsupported_display_language_choice_does_not_change_the_preference(string selectedLanguage)
    {
        using var webClient = _fixture.CreateHttpClient(allowAutoRedirect: false);
        var antiforgery = await GetAntiforgeryTokenAsync(webClient);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/display-language")
        {
            Content = new FormUrlEncodedContent(
            [
                new("locale", selectedLanguage),
                new("returnUrl", "/"),
                new("__RequestVerificationToken", antiforgery.Token),
            ]),
        };
        request.Headers.Add("Cookie", $"{antiforgery.Cookie}; ChronicleOfHeros.DisplayLanguage=en-US");

        using var response = await webClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(cookie => cookie.StartsWith("ChronicleOfHeros.DisplayLanguage=", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Tests that a display language choice submission with an unsafe return path redirects to the root path, verifying the response status and location header for correctness.
    /// </summary>
    /// <param name="returnUrl">The return URL specified in the display language choice submission.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-path")]
    [InlineData("https://example.com")]
    [InlineData("//example.com")]
    [InlineData("/\\example.com")]
    [InlineData("/%2F%2Fexample.com")]
    [InlineData("/%5Cexample.com")]
    [InlineData("/%252F%252Fexample.com")]
    public async Task Display_language_choice_with_an_unsafe_return_path_redirects_to_root(string? returnUrl)
    {
        using var webClient = _fixture.CreateHttpClient(allowAutoRedirect: false);
        var antiforgery = await GetAntiforgeryTokenAsync(webClient);
        var formValues = new List<KeyValuePair<string, string>>
        {
            new("locale", "de-DE"),
            new("__RequestVerificationToken", antiforgery.Token),
        };
        if (returnUrl is not null)
        {
            formValues.Add(new("returnUrl", returnUrl));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/display-language")
        {
            Content = new FormUrlEncodedContent(formValues),
        };
        request.Headers.Add("Cookie", antiforgery.Cookie);

        using var response = await webClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    /// <summary>
    /// Tests that the public root page presents the landing experience in German when the browser's language preference is set to German, verifying the title, document language, navigation, headings, and other elements for correctness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Public_root_initial_document_presents_the_landing_experience_in_German()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(baseAddress.AbsoluteUri).ConfigureAwait(false);

            await Assertions.Expect(page).ToHaveTitleAsync("ChronicleOfHeros | Dein Charakterbogen am Spieltisch").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("lang", "de").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Hauptnavigation" }).First).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByLabel("Navigationsmenü")).ToBeAttachedAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Ein präziser Charakterbogen, bereit für den Spieltisch." })).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Demnächst" })).ToBeDisabledAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("Rüstungsklasse", new() { Exact = true })).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("Bewegungsrate", new() { Exact = true })).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("30 ft.", new() { Exact = true })).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#how-it-works").GetByRole(AriaRole.Heading, new() { Name = "Verstehen" })).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#about").GetByRole(AriaRole.Heading, new() { Name = "Über ChronicleOfHeros" })).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("body")).Not.ToContainTextAsync("Character Sheets").ConfigureAwait(false);
        }, javaScriptEnabled: false, locale: "de-CH");
    }

    /// <summary>
    /// Tests that the public root page retains the German language preference after a page reload, verifying the title, headings, navigation, and absence of English content for correctness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Public_root_keeps_German_after_a_reload()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(baseAddress.AbsoluteUri).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);

            await page.ReloadAsync().ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);

            await Assertions.Expect(page).ToHaveTitleAsync("ChronicleOfHeros | Dein Charakterbogen am Spieltisch").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Ein präziser Charakterbogen, bereit für den Spieltisch." })).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Hauptnavigation" }).First).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("body")).Not.ToContainTextAsync("An accurate character sheet, ready at the table.").ConfigureAwait(false);
        }, locale: "de-CH");
    }

    /// <summary>
    /// Tests that the display language selector changes the language of the page when an option is selected, verifying the visibility of the selector, available options, selected value, URL, title, and persistence after a page reload for correctness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Display_language_selector_changes_language_when_an_option_is_selected()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(new Uri(baseAddress, "/?character-sheet").AbsoluteUri).ConfigureAwait(false);

            var selector = page.GetByLabel("Display language");

            await Assertions.Expect(selector).ToBeVisibleAsync().ConfigureAwait(false);
            Assert.Equal(["English", "Deutsch"], await selector.Locator("option").AllTextContentsAsync().ConfigureAwait(false));
            await selector.SelectOptionAsync("de-DE").ConfigureAwait(false);

            await Assertions.Expect(page).ToHaveURLAsync(new Regex("\\?character-sheet$")).ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveTitleAsync("ChronicleOfHeros | Dein Charakterbogen am Spieltisch").ConfigureAwait(false);
            await Assertions.Expect(page.GetByLabel("Anzeigesprache")).ToHaveValueAsync("de-DE").ConfigureAwait(false);

            await page.ReloadAsync().ConfigureAwait(false);

            await Assertions.Expect(page).ToHaveTitleAsync("ChronicleOfHeros | Dein Charakterbogen am Spieltisch").ConfigureAwait(false);
            await Assertions.Expect(page.GetByLabel("Anzeigesprache")).ToHaveValueAsync("de-DE").ConfigureAwait(false);
        }, locale: "en-US");
    }

    /// <summary>
    /// Tests that the display language selector is visible and functional on an unknown local route, allowing the user to change the language and retain the 404 status, verifying the visibility of the selector, presence of the anti-forgery token, selected value, URL, response status, title, heading, and selected value after a page reload for correctness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Display_language_selector_from_an_unknown_local_route_returns_there_with_a_404()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            const string unknownRoute = "/missing-character?record=unknown";
            await page.GotoAsync(new Uri(baseAddress, unknownRoute).AbsoluteUri).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);

            var selector = page.GetByLabel("Display language");
            await Assertions.Expect(selector).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("input[name='__RequestVerificationToken']")).ToHaveValueAsync(new Regex(".+")).ConfigureAwait(false);
            await selector.SelectOptionAsync("de-DE").ConfigureAwait(false);

            await Assertions.Expect(page).ToHaveURLAsync(new Uri(baseAddress, unknownRoute).AbsoluteUri).ConfigureAwait(false);
            var notFoundResponse = await page.ReloadAsync().ConfigureAwait(false);

            Assert.NotNull(notFoundResponse);
            Assert.Equal((int)HttpStatusCode.NotFound, notFoundResponse.Status);
            await Assertions.Expect(page).ToHaveTitleAsync("Seite nicht gefunden | ChronicleOfHeros").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Diese Seite fehlt im Register." })).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByLabel("Anzeigesprache")).ToHaveValueAsync("de-DE").ConfigureAwait(false);
        }, locale: "en-US");
    }

    /// <summary>
    /// Tests that the display language selector on an unknown local route is keyboard focusable, allowing users to navigate and interact with it using the keyboard, verifying the visibility and focus state of the selector for correctness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Display_language_selector_on_an_unknown_local_route_is_keyboard_focusable()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(new Uri(baseAddress, "/missing-character").AbsoluteUri).ConfigureAwait(false);

            var selector = page.GetByLabel("Display language");
            await Assertions.Expect(selector).ToBeVisibleAsync().ConfigureAwait(false);
            await selector.FocusAsync().ConfigureAwait(false);
            await Assertions.Expect(selector).ToBeFocusedAsync().ConfigureAwait(false);
        }, javaScriptEnabled: false, locale: "en-US");
    }

    /// <summary>
    /// Tests that the public root page excludes the default template presentation, ensuring that specific elements from the default template are not present on the landing page, verifying the absence of certain stylesheets, links, and text for correctness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Public_root_excludes_default_template_presentation()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(baseAddress.AbsoluteUri).ConfigureAwait(false);

            await Assertions.Expect(page.Locator("link[rel='stylesheet'][href*='bootstrap']")).ToHaveCountAsync(0).ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Counter", Exact = true })).ToHaveCountAsync(0).ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Weather", Exact = true })).ToHaveCountAsync(0).ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("Hello, world!", new() { Exact = true })).ToHaveCountAsync(0).ConfigureAwait(false);
            await Assertions.Expect(page.GetByText("ChronicleOfHeros.Web", new() { Exact = true })).ToHaveCountAsync(0).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Tests that the public root page explains the character management journey through on-page navigation, verifying the presence and attributes of navigation links, as well as the content of specific sections for correctness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Public_root_explains_the_character_management_journey_through_on_page_navigation()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(baseAddress.AbsoluteUri).ConfigureAwait(false);

            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Character Sheets" })).ToHaveAttributeAsync("href", "#character-sheet").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "How It Works" })).ToHaveAttributeAsync("href", "#how-it-works").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "About" })).ToHaveAttributeAsync("href", "#about").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#how-it-works article").Nth(0)).ToContainTextAsync("choices").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#how-it-works article").Nth(1)).ToContainTextAsync("derived values").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#how-it-works article").Nth(2)).ToContainTextAsync("level").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#about").GetByRole(AriaRole.Heading, new() { Name = "About ChronicleOfHeros" })).ToBeVisibleAsync().ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Tests that the narrow header navigation can be opened and closed using keyboard interactions, verifying the visibility of the navigation menu and the focus state of links for correctness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Narrow_header_navigation_opens_from_the_keyboard()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.SetViewportSizeAsync(320, 800).ConfigureAwait(false);
            await page.GotoAsync(baseAddress.AbsoluteUri).ConfigureAwait(false);

            var navigation = page.GetByRole(AriaRole.Navigation, new() { Name = "Primary navigation" });
            var menuButton = page.GetByLabel("Navigation menu");

            await Assertions.Expect(navigation).ToBeHiddenAsync().ConfigureAwait(false);

            await menuButton.FocusAsync().ConfigureAwait(false);
            await page.Keyboard.PressAsync("Enter").ConfigureAwait(false);

            await Assertions.Expect(navigation).ToBeVisibleAsync().ConfigureAwait(false);

            await page.Keyboard.PressAsync("Enter").ConfigureAwait(false);

            await Assertions.Expect(navigation).ToBeHiddenAsync().ConfigureAwait(false);

            await page.Keyboard.PressAsync("Enter").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Tab").ConfigureAwait(false);

            var homeNavigationLink = page.GetByRole(AriaRole.Navigation, new() { Name = "Primary navigation" })
                .GetByRole(AriaRole.Link, new() { Name = "Home", Exact = true });
            await Assertions.Expect(homeNavigationLink).ToBeFocusedAsync().ConfigureAwait(false);

            await page.Keyboard.PressAsync("Tab").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Enter").ConfigureAwait(false);

            await Assertions.Expect(page).ToHaveURLAsync(new Regex("#character-sheet$")).ConfigureAwait(false);
        }, javaScriptEnabled: false);
    }

    /// <summary>
    /// Tests that the narrow header navigation has a visible keyboard focus indicator when focused, verifying the focus state and visibility of the focus indicator for correctness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Narrow_header_navigation_has_visible_keyboard_focus()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.SetViewportSizeAsync(320, 800).ConfigureAwait(false);
            await page.GotoAsync(baseAddress.AbsoluteUri).ConfigureAwait(false);

            var menuButton = page.GetByLabel("Navigation menu");

            await menuButton.FocusAsync().ConfigureAwait(false);
            await Assertions.Expect(menuButton).ToBeFocusedAsync().ConfigureAwait(false);
            Assert.True(await HasVisibleFocusAsync(menuButton).ConfigureAwait(false));

            await page.Keyboard.PressAsync("Enter").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Tab").ConfigureAwait(false);

            var homeNavigationLink = page.GetByRole(AriaRole.Navigation, new() { Name = "Primary navigation" })
                .GetByRole(AriaRole.Link, new() { Name = "Home", Exact = true });
            await Assertions.Expect(homeNavigationLink).ToBeFocusedAsync().ConfigureAwait(false);
            Assert.True(await HasVisibleFocusAsync(homeNavigationLink).ConfigureAwait(false));
        }, javaScriptEnabled: false);
    }

    /// <summary>
    /// Tests that the public root page reduces nonessential motion when the user has requested reduced motion in their system preferences, verifying the media query match, transition duration, and relevant style properties for correctness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Public_root_reduces_nonessential_motion_when_requested()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(baseAddress.AbsoluteUri).ConfigureAwait(false);

            var navigationLink = page.GetByRole(AriaRole.Link, new() { Name = "Character Sheets" });

            await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce }).ConfigureAwait(false);

            var reducedMotionRequested = await page.EvaluateAsync<bool>(
                "() => matchMedia('(prefers-reduced-motion: reduce)').matches").ConfigureAwait(false);
            var reducedTransitionMilliseconds = await TransitionDurationMillisecondsAsync(navigationLink).ConfigureAwait(false);
            var reducedMotionDiagnostics = await navigationLink.EvaluateAsync<string>(
                "element => { const style = getComputedStyle(element); return JSON.stringify({ transitionProperty: style.transitionProperty, transitionDuration: style.transitionDuration, scopeAttributes: [...element.attributes].map(attribute => attribute.name).filter(name => name.startsWith('b-')) }); }").ConfigureAwait(false);
            Assert.True(reducedMotionRequested);
            Assert.True(reducedTransitionMilliseconds <= 1, reducedMotionDiagnostics);
        }, javaScriptEnabled: false);
    }

    /// <summary>
    /// Tests that the public root page has sufficient contrast for text and controls, verifying the contrast ratios of specific text elements and a control against the background color for correctness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Public_root_text_and_compact_control_have_sufficient_contrast()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.SetViewportSizeAsync(320, 800).ConfigureAwait(false);
            await page.GotoAsync(baseAddress.AbsoluteUri).ConfigureAwait(false);

            var contrastRatios = await page.EvaluateAsync<double[]>(
                """
                () => {
                    const parseColor = value => value.match(/\d+(?:\.\d+)?/g).slice(0, 3).map(Number);
                    const luminance = color => color
                        .map(channel => channel / 255)
                        .map(channel => channel <= 0.04045 ? channel / 12.92 : Math.pow((channel + 0.055) / 1.055, 2.4))
                        .reduce((total, channel, index) => total + channel * [0.2126, 0.7152, 0.0722][index], 0);
                    const ratio = (foreground, background) => {
                        const values = [luminance(foreground), luminance(background)].sort((left, right) => right - left);
                        return (values[0] + 0.05) / (values[1] + 0.05);
                    };
                    const paper = parseColor(getComputedStyle(document.querySelector('.landing-page')).backgroundColor);
                    const selectors = ['.landing-page h1', '.field-hero > div > p:not(.eyebrow)', '.eyebrow'];
                    const textRatios = selectors.map(selector => ratio(parseColor(getComputedStyle(document.querySelector(selector)).color), paper));
                    const controlRatio = ratio(parseColor(getComputedStyle(document.querySelector('.navigation-disclosure summary')).color), paper);
                    return [...textRatios, controlRatio];
                }
                """).ConfigureAwait(false);

            Assert.All(contrastRatios.Take(3), ratio => Assert.True(ratio >= 4.5, $"Expected text contrast of at least 4.5:1, but found {ratio:F2}:1."));
            Assert.True(contrastRatios[3] >= 3, $"Expected control contrast of at least 3:1, but found {contrastRatios[3]:F2}:1.");
        });
    }

    /// <summary>
    /// Tests that the public root page remains coherent and visually consistent across supported viewport widths, verifying the absence of horizontal overflow, clipped elements, and alignment of specific elements for correctness.
    /// </summary>
    /// <param name="viewportWidth">The width of the viewport to test.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Theory]
    [InlineData(320)]
    [InlineData(768)]
    [InlineData(1440)]
    public async Task Public_root_remains_coherent_at_supported_viewport_widths(int viewportWidth)
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.SetViewportSizeAsync(viewportWidth, 1000).ConfigureAwait(false);
            await page.GotoAsync(baseAddress.AbsoluteUri).ConfigureAwait(false);

            var hasHorizontalOverflow = await page.EvaluateAsync<bool>(
                "() => document.documentElement.scrollWidth > document.documentElement.clientWidth").ConfigureAwait(false);
            var clippedElementCount = await page.Locator(".landing-page *:visible").EvaluateAllAsync<int>(
                "(elements, width) => elements.filter(element => { const bounds = element.getBoundingClientRect(); return bounds.left < 0 || bounds.right > width; }).length",
                viewportWidth).ConfigureAwait(false);
            var recordValues = page.Locator("#character-sheet dl > div");

            Assert.False(hasHorizontalOverflow);
            Assert.Equal(0, clippedElementCount);
            await Assertions.Expect(recordValues).ToHaveCountAsync(3).ConfigureAwait(false);

            var valuePositions = await recordValues.EvaluateAllAsync<float[]>(
                "elements => elements.map(element => element.getBoundingClientRect().top)").ConfigureAwait(false);
            Assert.All(valuePositions, position => Assert.Equal(valuePositions[0], position));

            if (viewportWidth == 320)
            {
                var contentPositions = await page.Locator("#landing-title, #character-sheet, #how-it-works, #about")
                    .EvaluateAllAsync<float[]>("elements => elements.map(element => element.getBoundingClientRect().top)").ConfigureAwait(false);
                Assert.Equal(contentPositions.Order(), contentPositions);
            }
        });
    }

    /// <summary>
    /// Tests that an invalid URL presents a branded way back to the public root, verifying the title, navigation, headings, links, and display language selector for correctness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Invalid_url_presents_a_branded_way_back_to_the_public_root()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(new Uri(baseAddress, "/a-page-that-does-not-exist").AbsoluteUri).ConfigureAwait(false);

            await Assertions.Expect(page).ToHaveTitleAsync("Page not found | ChronicleOfHeros").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Primary navigation" })).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Home", Exact = true })).ToHaveAttributeAsync("href", "/").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "ChronicleOfHeros" })).ToHaveAttributeAsync("href", "/").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "This page is missing from the record." })).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Return to the character sheet" })).ToHaveAttributeAsync("href", "/").ConfigureAwait(false);
            await Assertions.Expect(page.GetByLabel("Display language")).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Not Found", Exact = true })).ToHaveCountAsync(0).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Tests that public demo routes are absent, ensuring that specific demo routes are not accessible and return a 404 response, verifying the title and heading for correctness.
    /// </summary>
    /// <param name="route">The public demo route to test for absence.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Theory]
    [InlineData("/counter")]
    [InlineData("/weather")]
    public async Task Public_demo_routes_are_absent(string route)
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(new Uri(baseAddress, route).AbsoluteUri).ConfigureAwait(false);

            await Assertions.Expect(page).ToHaveTitleAsync("Page not found | ChronicleOfHeros").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "This page is missing from the record." })).ToBeVisibleAsync().ConfigureAwait(false);
        });
    }

    private Task WithPublicPageAsync(
        Func<IPage, Uri, Task> exercisePage,
        bool javaScriptEnabled = true,
        string? locale = null) =>
        _fixture.WithPublicPageAsync(exercisePage, javaScriptEnabled, locale);

    private static Task<double> TransitionDurationMillisecondsAsync(ILocator locator) =>
        locator.EvaluateAsync<double>(
            "element => Math.max(...getComputedStyle(element).transitionDuration.split(',').map(value => value.endsWith('ms') ? parseFloat(value) : parseFloat(value) * 1000))");

    private static Task<bool> HasVisibleFocusAsync(ILocator locator) =>
        locator.EvaluateAsync<bool>(
            "element => { const style = getComputedStyle(element); return style.outlineStyle !== 'none' && parseFloat(style.outlineWidth) >= 2; }");

    private static async Task<(string Token, string Cookie)> GetAntiforgeryTokenAsync(HttpClient webClient)
    {
        using var response = await webClient.GetAsync("/", TestContext.Current.CancellationToken).ConfigureAwait(false);
        var landingPage = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        var tokenMatch = Regex.Match(
            landingPage,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
            RegexOptions.CultureInvariant);
        var antiforgeryCookie = response.Headers.GetValues("Set-Cookie")
            .Select(value => value.Split(';', 2)[0])
            .Single(value => value.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));

        Assert.True(tokenMatch.Success, "The response did not include an antiforgery token.");
        return (WebUtility.HtmlDecode(tokenMatch.Groups["token"].Value), antiforgeryCookie);
    }
}