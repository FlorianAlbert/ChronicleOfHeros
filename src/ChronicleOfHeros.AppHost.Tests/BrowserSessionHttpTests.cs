using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Playwright;

namespace ChronicleOfHeros.AppHost.Tests;

/// <summary>
/// HTTP integration tests for the browser-facing Player session.
/// </summary>
[Collection("AppHost integration")]
public sealed class BrowserSessionHttpTests(LandingPageFixture fixture) : IClassFixture<LandingPageFixture>
{
    private readonly LandingPageFixture _fixture = fixture;

#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Verifies that the static sign-in form is localized for the Player's display language.
    /// </summary>
    /// <param name="browserLanguage">The browser language preference.</param>
    /// <param name="expectedCulture">The expected display culture.</param>
    /// <param name="expectedHeading">The expected sign-in heading.</param>
    [Theory]
    [InlineData("en-US", "en-US", "Sign in")]
    [InlineData("de-DE", "de-DE", "Anmelden")]
    public async Task Sign_in_form_is_available_in_the_Players_display_language(
        string browserLanguage,
        string expectedCulture,
        string expectedHeading)
    {
        using HttpClient webClient = _fixture.CreateHttpClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/sign-in");
        request.Headers.AcceptLanguage.ParseAdd(browserLanguage);

        using HttpResponseMessage response = await webClient.SendAsync(
            request,
            TestContext.Current.CancellationToken);
        string signInPage = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([expectedCulture], response.Content.Headers.ContentLanguage);
        Assert.Matches(
            """<form(?=[^>]*\bmethod="post")(?=[^>]*\baction="/sign-in/submit")[^>]*>""",
            signInPage);
        Assert.Matches($"<h1[^>]*>{expectedHeading}</h1>", signInPage);
        Assert.Contains("__RequestVerificationToken", signInPage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a normal Player signs in through the browser form and receives only an opaque session cookie.
    /// </summary>
    [Fact]
    public async Task Normal_Player_sign_in_creates_an_opaque_browser_session_and_reaches_the_localized_home()
    {
        using HttpClient apiClient = _fixture.CreateApiClient();
        (string username, string password) = await CreateNormalPlayerAsync(apiClient);
        using HttpClient webClient = _fixture.CreateHttpClient(allowAutoRedirect: false);
        (string antiforgeryToken, string antiforgeryCookie) = await GetAntiforgeryTokenAsync(
            webClient,
            "de-DE");

        using HttpRequestMessage signInRequest = new(HttpMethod.Post, "/sign-in/submit")
        {
            Content = new FormUrlEncodedContent(
            [
                new("username", username),
                new("password", password),
                new("returnUrl", "/player"),
                new("__RequestVerificationToken", antiforgeryToken),
            ]),
        };
        signInRequest.Headers.Add("Cookie", antiforgeryCookie);

        using HttpResponseMessage signInResponse = await webClient.SendAsync(
            signInRequest,
            TestContext.Current.CancellationToken);
        string signInBody = await signInResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        string sessionCookie = signInResponse.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("__Host-ChronicleOfHeros.Session=", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.SeeOther, signInResponse.StatusCode);
        Assert.Equal("/player", signInResponse.Headers.Location?.OriginalString);
        Assert.StartsWith("__Host-ChronicleOfHeros.Session=", sessionCookie, StringComparison.Ordinal);
        Assert.Contains("path=/", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max-age=", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accessToken", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accessToken", signInBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", signInBody, StringComparison.OrdinalIgnoreCase);

        string sessionCookieHeader = sessionCookie[..sessionCookie.IndexOf(';', StringComparison.Ordinal)];
        using HttpRequestMessage playerHomeRequest = new(HttpMethod.Get, "/player");
        playerHomeRequest.Headers.AcceptLanguage.ParseAdd("de-DE");
        playerHomeRequest.Headers.Add("Cookie", sessionCookieHeader);
        using HttpResponseMessage playerHomeResponse = await webClient.SendAsync(
            playerHomeRequest,
            TestContext.Current.CancellationToken);
        string playerHome = await playerHomeResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, playerHomeResponse.StatusCode);
        Assert.Equal(["de-DE"], playerHomeResponse.Content.Headers.ContentLanguage);
        Assert.Contains("Dein Charakterbogen", playerHome, StringComparison.Ordinal);
        Assert.DoesNotContain("accessToken", playerHome, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", playerHome, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that the BFF uses the browser session's server-held bearer token and never exposes the generic API route.
    /// </summary>
    [Fact]
    public async Task Browser_BFF_ignores_supplied_bearer_credentials_and_uses_the_server_held_token()
    {
        using HttpClient apiClient = _fixture.CreateApiClient();
        (string username, string password) = await CreateNormalPlayerAsync(apiClient);
        using HttpClient webClient = _fixture.CreateHttpClient(allowAutoRedirect: false);
        string sessionCookie = await CreateBrowserSessionAsync(webClient, username, password);

        using HttpRequestMessage bffRequest = new(HttpMethod.Get, "/bff/players/me");
        bffRequest.Headers.Add("Cookie", sessionCookie);
        bffRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "browser-supplied-credential");
        using HttpResponseMessage bffResponse = await webClient.SendAsync(
            bffRequest,
            TestContext.Current.CancellationToken);
        string bffBody = await bffResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, bffResponse.StatusCode);
        Assert.DoesNotContain("accessToken", bffBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", bffBody, StringComparison.OrdinalIgnoreCase);
        Assert.False(bffResponse.Headers.Contains("Set-Cookie"));

        using HttpRequestMessage genericProxyRequest = new(HttpMethod.Get, "/api/players/me");
        genericProxyRequest.Headers.Add("Cookie", sessionCookie);
        using HttpResponseMessage genericProxyResponse = await webClient.SendAsync(
            genericProxyRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, genericProxyResponse.StatusCode);
    }

    /// <summary>
    /// Verifies that rejected sign-in submissions cannot create a browser session.
    /// </summary>
    [Fact]
    public async Task Invalid_credentials_and_missing_antiforgery_proof_do_not_create_a_browser_session()
    {
        using HttpClient apiClient = _fixture.CreateApiClient();
        (string username, string password) = await CreateNormalPlayerAsync(apiClient);
        using HttpClient webClient = _fixture.CreateHttpClient(allowAutoRedirect: false);
        (string antiforgeryToken, string antiforgeryCookie) = await GetAntiforgeryTokenAsync(webClient, "en-US");

        using HttpRequestMessage invalidCredentialsRequest = new(HttpMethod.Post, "/sign-in/submit")
        {
            Content = new FormUrlEncodedContent(
            [
                new("username", username),
                new("password", "wrong-password"),
                new("__RequestVerificationToken", antiforgeryToken),
            ]),
        };
        invalidCredentialsRequest.Headers.Add("Cookie", antiforgeryCookie);
        using HttpResponseMessage invalidCredentialsResponse = await webClient.SendAsync(
            invalidCredentialsRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, invalidCredentialsResponse.StatusCode);
        Assert.False(invalidCredentialsResponse.Headers.TryGetValues("Set-Cookie", out _));

        using HttpRequestMessage missingAntiforgeryRequest = new(HttpMethod.Post, "/sign-in/submit")
        {
            Content = new FormUrlEncodedContent(
            [
                new("username", username),
                new("password", password),
            ]),
        };
        using HttpResponseMessage missingAntiforgeryResponse = await webClient.SendAsync(
            missingAntiforgeryRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, missingAntiforgeryResponse.StatusCode);
        Assert.False(missingAntiforgeryResponse.Headers.TryGetValues("Set-Cookie", out _));
    }

    /// <summary>
    /// Verifies that unsafe continuations are replaced with the local Player home and authenticated entry points redirect there.
    /// </summary>
    [Theory]
    [InlineData("https://untrusted.example/continuation")]
    [InlineData("/sign-in")]
    [InlineData("/%73ign-in")]
    public async Task Browser_sign_in_safely_falls_back_from_unsafe_continuations_and_authenticated_entry_points(
        string continuation)
    {
        using HttpClient apiClient = _fixture.CreateApiClient();
        (string username, string password) = await CreateNormalPlayerAsync(apiClient);
        using HttpClient webClient = _fixture.CreateHttpClient(allowAutoRedirect: false);
        string sessionCookie = await CreateBrowserSessionAsync(
            webClient,
            username,
            password,
            continuation);

        foreach (string publicEntryPoint in new[] { "/", "/sign-in" })
        {
            using HttpRequestMessage request = new(HttpMethod.Get, publicEntryPoint);
            request.Headers.Add("Cookie", sessionCookie);
            using HttpResponseMessage response = await webClient.SendAsync(
                request,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
            Assert.Equal("/player", response.Headers.Location?.OriginalString);
        }
    }

    /// <summary>
    /// Verifies that a Player can complete the localized static-SSR sign-in journey in a browser.
    /// </summary>
    [Fact]
    public async Task Normal_Player_can_complete_the_German_browser_sign_in_journey()
    {
        using HttpClient apiClient = _fixture.CreateApiClient();
        (string username, string password) = await CreateNormalPlayerAsync(apiClient);

        await _fixture.WithPublicPageAsync(async (page, baseAddress) =>
        {
            _ = await page.GotoAsync(new Uri(baseAddress, "/sign-in").AbsoluteUri).ConfigureAwait(false);
            await page.GetByLabel("Benutzername").FillAsync(username).ConfigureAwait(false);
            await page.GetByLabel("Passwort").FillAsync(password).ConfigureAwait(false);
            await page.GetByRole(AriaRole.Button, new() { Name = "Weiter" }).ClickAsync().ConfigureAwait(false);

            await Assertions.Expect(page).ToHaveURLAsync(new Uri(baseAddress, "/player").AbsoluteUri).ConfigureAwait(false);
            await Assertions.Expect(page).ToHaveTitleAsync("Dein Charakterbogen | ChronicleOfHeros").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Dein Charakterbogen" })).ToBeVisibleAsync().ConfigureAwait(false);
            string playerHome = await page.ContentAsync().ConfigureAwait(false);
            Assert.DoesNotContain("accessToken", playerHome, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("refreshToken", playerHome, StringComparison.OrdinalIgnoreCase);
        }, javaScriptEnabled: false, locale: "de-DE");
    }

    private static async Task<(string Username, string Password)> CreateNormalPlayerAsync(HttpClient apiClient)
    {
        string? operatorTemporaryAccessToken = await TrySignInAndGetAccessTokenAsync(
            apiClient,
            BootstrapOperatorTestParameters.Username,
            BootstrapOperatorTestParameters.TemporaryPassword).ConfigureAwait(false);
        const string operatorPassword = "Browser-operator-password1!";
        string operatorAccessToken;
        if (operatorTemporaryAccessToken is not null)
        {
            await ChangePasswordAsync(
                apiClient,
                operatorTemporaryAccessToken,
                BootstrapOperatorTestParameters.TemporaryPassword,
                operatorPassword).ConfigureAwait(false);
            operatorAccessToken = await SignInAndGetAccessTokenAsync(
                apiClient,
                BootstrapOperatorTestParameters.Username,
                operatorPassword).ConfigureAwait(false);
        }
        else
        {
            operatorAccessToken = await SignInAndGetAccessTokenAsync(
                apiClient,
                BootstrapOperatorTestParameters.Username,
                operatorPassword).ConfigureAwait(false);
        }

        string username = $"Browser{Guid.NewGuid():N}"[..27];
        using HttpRequestMessage enrollmentRequest = new(HttpMethod.Post, "/players")
        {
            Content = JsonContent.Create(new { Username = username }),
        };
        enrollmentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorAccessToken);
        using HttpResponseMessage enrollmentResponse = await apiClient.SendAsync(
            enrollmentRequest,
            TestContext.Current.CancellationToken).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Created, enrollmentResponse.StatusCode);

        using JsonDocument enrollmentBody = await JsonDocument.ParseAsync(
            await enrollmentResponse.Content.ReadAsStreamAsync(
                TestContext.Current.CancellationToken).ConfigureAwait(false),
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false);
        string temporaryCredential = enrollmentBody.RootElement.GetProperty("temporaryCredential").GetString()
            ?? throw new InvalidOperationException("Player enrollment did not return a temporary credential.");
        string restrictedPlayerAccessToken = await SignInAndGetAccessTokenAsync(
            apiClient,
            username,
            temporaryCredential).ConfigureAwait(false);
        const string playerPassword = "Browser-player-password1!";
        await ChangePasswordAsync(
            apiClient,
            restrictedPlayerAccessToken,
            temporaryCredential,
            playerPassword).ConfigureAwait(false);

        return (username, playerPassword);
    }

    private static async Task<string> SignInAndGetAccessTokenAsync(HttpClient apiClient, string username, string password)
    {
        string? accessToken = await TrySignInAndGetAccessTokenAsync(
            apiClient,
            username,
            password).ConfigureAwait(false);
        return accessToken ?? throw new InvalidOperationException("Sign-in did not return an access token.");
    }

    private static async Task<string?> TrySignInAndGetAccessTokenAsync(HttpClient apiClient, string username, string password)
    {
        using HttpResponseMessage response = await apiClient.PostAsJsonAsync(
            "/authentication/sign-in",
            new
            {
                username,
                password,
            },
            TestContext.Current.CancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(
                TestContext.Current.CancellationToken).ConfigureAwait(false),
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.RootElement.GetProperty("accessToken").GetString();
    }

    private static async Task ChangePasswordAsync(
        HttpClient apiClient,
        string accessToken,
        string currentPassword,
        string newPassword)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/authentication/change-password")
        {
            Content = JsonContent.Create(new
            {
                currentPassword,
                newPassword,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using HttpResponseMessage response = await apiClient.SendAsync(
            request,
            TestContext.Current.CancellationToken).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<(string Token, string Cookie)> GetAntiforgeryTokenAsync(
        HttpClient webClient,
        string browserLanguage)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/sign-in");
        request.Headers.AcceptLanguage.ParseAdd(browserLanguage);
        using HttpResponseMessage response = await webClient.SendAsync(
            request,
            TestContext.Current.CancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken).ConfigureAwait(false);
        Match tokenMatch = Regex.Match(
            body,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
            RegexOptions.CultureInvariant);

        Assert.True(tokenMatch.Success);
        string antiforgeryCookie = response.Headers.GetValues("Set-Cookie")
            .Select(value => value.Split(';', 2)[0])
            .Single(value => value.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));
        return (WebUtility.HtmlDecode(tokenMatch.Groups["token"].Value), antiforgeryCookie);
    }

    private static async Task<string> CreateBrowserSessionAsync(
        HttpClient webClient,
        string username,
        string password,
        string? returnUrl = null)
    {
        (string antiforgeryToken, string antiforgeryCookie) = await GetAntiforgeryTokenAsync(
            webClient,
            "en-US").ConfigureAwait(false);
        using HttpRequestMessage signInRequest = new(HttpMethod.Post, "/sign-in/submit")
        {
            Content = new FormUrlEncodedContent(
            [
                new("username", username),
                new("password", password),
                new("__RequestVerificationToken", antiforgeryToken),
                new("returnUrl", returnUrl ?? string.Empty),
            ]),
        };
        signInRequest.Headers.Add("Cookie", antiforgeryCookie);

        using HttpResponseMessage signInResponse = await webClient.SendAsync(
            signInRequest,
            TestContext.Current.CancellationToken).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.SeeOther, signInResponse.StatusCode);
        Assert.Equal("/", signInResponse.Headers.Location?.OriginalString);
        return signInResponse.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("__Host-ChronicleOfHeros.Session=", StringComparison.Ordinal))
            .Split(';', 2)[0];
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores
}
