using Microsoft.Playwright;
using System.Net;
using System.Text.RegularExpressions;

namespace ChronicleOfHeros.AppHost.Tests;

[Collection("AppHost integration")]
public class LandingPageBrowserTests : IClassFixture<LandingPageFixture>
{
    private readonly LandingPageFixture _fixture;

    public LandingPageBrowserTests(LandingPageFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Public_root_presents_the_field_notes_landing_core_in_a_browser()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(baseAddress.AbsoluteUri);

            await Assertions.Expect(page).ToHaveTitleAsync("ChronicleOfHeros | Your character sheet at the table");
            await Assertions.Expect(page.Locator("link[rel='icon']")).ToHaveAttributeAsync("href", "favicon.svg");
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "An accurate character sheet, ready at the table." })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Armor", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Initiative", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Speed", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Coming soon" })).ToBeDisabledAsync();
            await Assertions.Expect(page.GetByLabel("Prototype variant selector")).ToHaveCountAsync(0);
        });
    }

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-path")]
    [InlineData("https://example.com")]
    [InlineData("//example.com")]
    [InlineData("/\\example.com")]
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

    [Fact]
    public async Task Public_root_initial_document_presents_the_landing_experience_in_German()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(baseAddress.AbsoluteUri);

            await Assertions.Expect(page).ToHaveTitleAsync("ChronicleOfHeros | Dein Charakterbogen am Spieltisch");
            await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("lang", "de");
            await Assertions.Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Hauptnavigation" }).First).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByLabel("Navigationsmenü")).ToBeAttachedAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Ein präziser Charakterbogen, bereit für den Spieltisch." })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Demnächst" })).ToBeDisabledAsync();
            await Assertions.Expect(page.GetByText("Rüstungsklasse", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Bewegungsrate", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("30 ft.", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("#how-it-works").GetByRole(AriaRole.Heading, new() { Name = "Verstehen" })).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("#about").GetByRole(AriaRole.Heading, new() { Name = "Über ChronicleOfHeros" })).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("body")).Not.ToContainTextAsync("Character Sheets");
        }, javaScriptEnabled: false, locale: "de-CH");
    }

    [Fact]
    public async Task Public_root_keeps_German_when_the_interactive_UI_starts()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(baseAddress.AbsoluteUri);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await page.ReloadAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await Assertions.Expect(page).ToHaveTitleAsync("ChronicleOfHeros | Dein Charakterbogen am Spieltisch");
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Ein präziser Charakterbogen, bereit für den Spieltisch." })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Hauptnavigation" }).First).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("body")).Not.ToContainTextAsync("An accurate character sheet, ready at the table.");
        }, locale: "de-CH");
    }

    [Fact]
    public async Task Display_language_selector_submits_from_the_keyboard_and_returns_to_the_current_page()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(new Uri(baseAddress, "/?character-sheet").AbsoluteUri);

            var selector = page.GetByLabel("Display language");
            var applyButton = page.GetByRole(AriaRole.Button, new() { Name = "Apply" });

            await Assertions.Expect(selector).ToBeVisibleAsync();
            Assert.Equal(["English", "Deutsch"], await selector.Locator("option").AllTextContentsAsync());
            await selector.FocusAsync();
            await Assertions.Expect(selector).ToBeFocusedAsync();
            await page.Keyboard.PressAsync("ArrowDown");
            await page.Keyboard.PressAsync("Tab");
            await Assertions.Expect(applyButton).ToBeFocusedAsync();
            await page.Keyboard.PressAsync("Enter");

            await Assertions.Expect(page).ToHaveURLAsync(new Regex("\\?character-sheet$"));
            await Assertions.Expect(page).ToHaveTitleAsync("ChronicleOfHeros | Dein Charakterbogen am Spieltisch");
            await Assertions.Expect(page.GetByLabel("Anzeigesprache")).ToBeVisibleAsync();

            await page.ReloadAsync();

            await Assertions.Expect(page).ToHaveTitleAsync("ChronicleOfHeros | Dein Charakterbogen am Spieltisch");
            await Assertions.Expect(page.GetByLabel("Anzeigesprache")).ToBeVisibleAsync();
        }, javaScriptEnabled: false, locale: "en-US");
    }

    [Fact]
    public async Task Public_root_excludes_default_template_presentation()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(baseAddress.AbsoluteUri);

            await Assertions.Expect(page.Locator("link[rel='stylesheet'][href*='bootstrap']")).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Home", Exact = true })).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Counter", Exact = true })).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Weather", Exact = true })).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByText("Hello, world!", new() { Exact = true })).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByText("ChronicleOfHeros.Web", new() { Exact = true })).ToHaveCountAsync(0);
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
    public async Task Narrow_header_navigation_opens_from_the_keyboard()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.SetViewportSizeAsync(320, 800);
            await page.GotoAsync(baseAddress.AbsoluteUri);

            var navigation = page.GetByRole(AriaRole.Navigation, new() { Name = "Primary navigation" });
            var menuButton = page.GetByLabel("Navigation menu");

            await Assertions.Expect(navigation).ToBeHiddenAsync();

            await menuButton.FocusAsync();
            await page.Keyboard.PressAsync("Enter");

            await Assertions.Expect(navigation).ToBeVisibleAsync();

            await page.Keyboard.PressAsync("Enter");

            await Assertions.Expect(navigation).ToBeHiddenAsync();

            await page.Keyboard.PressAsync("Enter");
            await page.Keyboard.PressAsync("Tab");
            await page.Keyboard.PressAsync("Enter");

            await Assertions.Expect(page).ToHaveURLAsync(new Regex("#character-sheet$"));
        }, javaScriptEnabled: false);
    }

    [Fact]
    public async Task Narrow_header_navigation_has_visible_keyboard_focus()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.SetViewportSizeAsync(320, 800);
            await page.GotoAsync(baseAddress.AbsoluteUri);

            var menuButton = page.GetByLabel("Navigation menu");

            await menuButton.FocusAsync();
            await Assertions.Expect(menuButton).ToBeFocusedAsync();
            Assert.True(await HasVisibleFocusAsync(menuButton));

            await page.Keyboard.PressAsync("Enter");
            await page.Keyboard.PressAsync("Tab");

            var firstNavigationLink = page.GetByRole(AriaRole.Navigation, new() { Name = "Primary navigation" })
                .GetByRole(AriaRole.Link, new() { Name = "Character Sheets" });
            await Assertions.Expect(firstNavigationLink).ToBeFocusedAsync();
            Assert.True(await HasVisibleFocusAsync(firstNavigationLink));
        }, javaScriptEnabled: false);
    }

    [Fact]
    public async Task Public_root_reduces_nonessential_motion_when_requested()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(baseAddress.AbsoluteUri);

            var navigationLink = page.GetByRole(AriaRole.Link, new() { Name = "Character Sheets" });

            await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });

            var reducedMotionRequested = await page.EvaluateAsync<bool>(
                "() => matchMedia('(prefers-reduced-motion: reduce)').matches");
            var reducedTransitionMilliseconds = await TransitionDurationMillisecondsAsync(navigationLink);
            var reducedMotionDiagnostics = await navigationLink.EvaluateAsync<string>(
                "element => { const style = getComputedStyle(element); return JSON.stringify({ transitionProperty: style.transitionProperty, transitionDuration: style.transitionDuration, scopeAttributes: [...element.attributes].map(attribute => attribute.name).filter(name => name.startsWith('b-')) }); }");
            Assert.True(reducedMotionRequested);
            Assert.True(reducedTransitionMilliseconds <= 1, reducedMotionDiagnostics);
        }, javaScriptEnabled: false);
    }

    [Fact]
    public async Task Public_root_text_and_compact_control_have_sufficient_contrast()
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.SetViewportSizeAsync(320, 800);
            await page.GotoAsync(baseAddress.AbsoluteUri);

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
                """);

            Assert.All(contrastRatios.Take(3), ratio => Assert.True(ratio >= 4.5, $"Expected text contrast of at least 4.5:1, but found {ratio:F2}:1."));
            Assert.True(contrastRatios[3] >= 3, $"Expected control contrast of at least 3:1, but found {contrastRatios[3]:F2}:1.");
        });
    }

    [Theory]
    [InlineData(320)]
    [InlineData(768)]
    [InlineData(1440)]
    public async Task Public_root_remains_coherent_at_supported_viewport_widths(int viewportWidth)
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.SetViewportSizeAsync(viewportWidth, 1000);
            await page.GotoAsync(baseAddress.AbsoluteUri);

            var hasHorizontalOverflow = await page.EvaluateAsync<bool>(
                "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
            var clippedElementCount = await page.Locator(".landing-page *:visible").EvaluateAllAsync<int>(
                "(elements, width) => elements.filter(element => { const bounds = element.getBoundingClientRect(); return bounds.left < 0 || bounds.right > width; }).length",
                viewportWidth);
            var recordValues = page.Locator("#character-sheet dl > div");

            Assert.False(hasHorizontalOverflow);
            Assert.Equal(0, clippedElementCount);
            await Assertions.Expect(recordValues).ToHaveCountAsync(3);

            var valuePositions = await recordValues.EvaluateAllAsync<float[]>(
                "elements => elements.map(element => element.getBoundingClientRect().top)");
            Assert.All(valuePositions, position => Assert.Equal(valuePositions[0], position));

            if (viewportWidth == 320)
            {
                var contentPositions = await page.Locator("#landing-title, #character-sheet, #how-it-works, #about")
                    .EvaluateAllAsync<float[]>("elements => elements.map(element => element.getBoundingClientRect().top)");
                Assert.Equal(contentPositions.Order(), contentPositions);
            }
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
            await Assertions.Expect(page.GetByLabel("Display language")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Not Found", Exact = true })).ToHaveCountAsync(0);
        });
    }

    [Theory]
    [InlineData("/counter")]
    [InlineData("/weather")]
    public async Task Public_demo_routes_are_absent(string route)
    {
        await WithPublicPageAsync(async (page, baseAddress) =>
        {
            await page.GotoAsync(new Uri(baseAddress, route).AbsoluteUri);

            await Assertions.Expect(page).ToHaveTitleAsync("Page not found | ChronicleOfHeros");
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "This page is missing from the record." })).ToBeVisibleAsync();
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
        using var response = await webClient.GetAsync("/", TestContext.Current.CancellationToken);
        var landingPage = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
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