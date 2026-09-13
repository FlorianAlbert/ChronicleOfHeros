using System.Net;

namespace ChronicleOfHeros.AppHost.Tests;

/// <summary>
/// This test class is intended to be a smoke test for the ChronicleOfHeros.AppHost application.
/// </summary>
[Collection("AppHost integration")]
public class AppHostSmokeTests(LandingPageFixture fixture) : IClassFixture<LandingPageFixture>
{
    private readonly LandingPageFixture _fixture = fixture;

#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// This test verifies that the public root of the application presents the field notes landing core.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Public_root_presents_the_field_notes_landing_core()
    {
        using HttpClient webClient = _fixture.CreateHttpClient();

        Uri landingPageUri = new("/", UriKind.Relative);
        string landingPage = await webClient.GetStringAsync(landingPageUri, TestContext.Current.CancellationToken);

        Assert.Contains("<title>ChronicleOfHeros | Your character sheet at the table</title>", landingPage, StringComparison.Ordinal);
        Assert.Contains("An accurate character sheet, ready at the table.", landingPage, StringComparison.Ordinal);
        Assert.Contains(">Armor<", landingPage, StringComparison.Ordinal);
        Assert.Contains(">Initiative<", landingPage, StringComparison.Ordinal);
        Assert.Contains(">Speed<", landingPage, StringComparison.Ordinal);
        Assert.Matches("""<a(?=[^>]*\bhref="/sign-in")[^>]*>Sign in</a>""", landingPage);
        Assert.DoesNotContain("prototype-switcher", landingPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Visual Prototype", landingPage, StringComparison.Ordinal);
    }

    /// <summary>
    /// This test verifies that the Web health endpoint is available while generic browser API forwarding is absent.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Health_endpoints_are_available_through_the_web_host()
    {
        using HttpClient webClient = _fixture.CreateHttpClient();

        Uri webHealthUri = new("/health", UriKind.Relative);
        using HttpResponseMessage webHealthResponse = await webClient.GetAsync(webHealthUri, TestContext.Current.CancellationToken);
        Uri genericApiHealthUri = new("/api/health", UriKind.Relative);
        using HttpResponseMessage genericApiHealthResponse = await webClient.GetAsync(
            genericApiHealthUri,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, webHealthResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, genericApiHealthResponse.StatusCode);
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores

}