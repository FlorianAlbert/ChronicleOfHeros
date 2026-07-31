using Microsoft.Playwright;
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

            await page.Keyboard.PressAsync("Tab");
            await page.Keyboard.PressAsync("Tab");

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
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Not Found", Exact = true })).ToHaveCountAsync(0);
        });
    }

    private Task WithPublicPageAsync(
        Func<IPage, Uri, Task> exercisePage,
        bool javaScriptEnabled = true) =>
        _fixture.WithPublicPageAsync(exercisePage, javaScriptEnabled);

    private static Task<double> TransitionDurationMillisecondsAsync(ILocator locator) =>
        locator.EvaluateAsync<double>(
            "element => Math.max(...getComputedStyle(element).transitionDuration.split(',').map(value => value.endsWith('ms') ? parseFloat(value) : parseFloat(value) * 1000))");

    private static Task<bool> HasVisibleFocusAsync(ILocator locator) =>
        locator.EvaluateAsync<bool>(
            "element => { const style = getComputedStyle(element); return style.outlineStyle !== 'none' && parseFloat(style.outlineWidth) >= 2; }");
}