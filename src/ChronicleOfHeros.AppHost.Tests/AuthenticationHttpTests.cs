using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ChronicleOfHeros.AppHost.Tests;

/// <summary>
/// Integration tests for the authentication endpoints of the ChronicleOfHeros application host.
/// </summary>
[Collection("AppHost integration")]
public sealed class AuthenticationHttpTests(AuthenticationAppFixture fixture) : IClassFixture<AuthenticationAppFixture>
{
    private readonly AuthenticationAppFixture _fixture = fixture;
    private const string ReplacementPassword = AuthenticationAppFixture.OperatorPassword;

#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Tests that an operator can enroll a new player with a temporary credential that requires replacement, and that the player can sign in with the temporary credential and does not receive a refresh token.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Operator_can_enroll_a_player_with_a_temporary_credential_that_requires_replacement()
    {
        using HttpClient apiClient = _fixture.CreateApiClient();
        TokenPair operatorTokenPair = await SignInAndGetTokenPairAsync(apiClient, AuthenticationAppFixture.OperatorPassword);

        using HttpRequestMessage enrollmentRequest = new(HttpMethod.Post, "/players")
        {
            Content = JsonContent.Create(new { Username = "EnrolledPlayer" }),
        };
        enrollmentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorTokenPair.AccessToken);
        using HttpResponseMessage enrollmentResponse = await apiClient.SendAsync(enrollmentRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, enrollmentResponse.StatusCode);
        Assert.Matches("^/players/[0-9a-f-]+$", enrollmentResponse.Headers.Location?.OriginalString);
        Assert.False(enrollmentResponse.Headers.Contains("Set-Cookie"));

        using JsonDocument enrollmentBody = await JsonDocument.ParseAsync(
            await enrollmentResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        string? temporaryCredential = enrollmentBody.RootElement.GetProperty("temporaryCredential").GetString();
        Assert.NotNull(temporaryCredential);

        using HttpResponseMessage playerSignInResponse = await SignInAsync(apiClient, "enrolledplayer", temporaryCredential);
        Assert.Equal(HttpStatusCode.OK, playerSignInResponse.StatusCode);
        using JsonDocument playerSignInBody = await JsonDocument.ParseAsync(
            await playerSignInResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(playerSignInBody.RootElement.TryGetProperty("refreshToken", out _));
    }

    /// <summary>
    /// Tests that when an operator resets a player's password, a new temporary credential is issued and all of the player's existing refresh sessions are revoked.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Operator_password_reset_issues_a_temporary_credential_and_revokes_the_players_refresh_sessions()
    {
        using HttpClient apiClient = _fixture.CreateApiClient();
        TokenPair operatorTokenPair = await SignInAndGetTokenPairAsync(apiClient, AuthenticationAppFixture.OperatorPassword);

        using HttpRequestMessage enrollmentRequest = new(HttpMethod.Post, "/players")
        {
            Content = JsonContent.Create(new { Username = "ResettablePlayer" }),
        };
        enrollmentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorTokenPair.AccessToken);
        using HttpResponseMessage enrollmentResponse = await apiClient.SendAsync(enrollmentRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, enrollmentResponse.StatusCode);
        using JsonDocument enrollmentBody = await JsonDocument.ParseAsync(
            await enrollmentResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        string? initialCredential = enrollmentBody.RootElement.GetProperty("temporaryCredential").GetString();
        Assert.NotNull(initialCredential);

        string restrictedPlayerAccessToken = await SignInAndGetAccessTokenAsync(
            apiClient,
            "ResettablePlayer",
            initialCredential);
        await ChangePasswordAsync(
            apiClient,
            restrictedPlayerAccessToken,
            initialCredential,
            "Original-player-password1!");
        TokenPair playerTokenPair = await SignInAndGetTokenPairAsync(
            apiClient,
            "ResettablePlayer",
            "Original-player-password1!");
        TokenPair secondPlayerTokenPair = await SignInAndGetTokenPairAsync(
            apiClient,
            "ResettablePlayer",
            "Original-player-password1!");

        using HttpRequestMessage resetRequest = new(HttpMethod.Post, "/players/ResettablePlayer/reset-password");
        resetRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorTokenPair.AccessToken);
        using HttpResponseMessage resetResponse = await apiClient.SendAsync(resetRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        Assert.False(resetResponse.Headers.Contains("Set-Cookie"));
        using JsonDocument resetBody = await JsonDocument.ParseAsync(
            await resetResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        string? resetCredential = resetBody.RootElement.GetProperty("temporaryCredential").GetString();
        Assert.NotNull(resetCredential);
        Assert.NotEqual(initialCredential, resetCredential);

        using HttpResponseMessage revokedRefreshResponse = await RefreshRequestAsync(apiClient, playerTokenPair.RefreshToken);
        using HttpResponseMessage secondRevokedRefreshResponse = await RefreshRequestAsync(
            apiClient,
            secondPlayerTokenPair.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedRefreshResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, secondRevokedRefreshResponse.StatusCode);

        using HttpResponseMessage resetCredentialSignInResponse = await SignInAsync(apiClient, "resettableplayer", resetCredential);
        Assert.Equal(HttpStatusCode.OK, resetCredentialSignInResponse.StatusCode);
        using JsonDocument resetCredentialSignInBody = await JsonDocument.ParseAsync(
            await resetCredentialSignInResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(resetCredentialSignInBody.RootElement.TryGetProperty("refreshToken", out _));
    }

    /// <summary>
    /// Tests that a player-only caller cannot enroll new players or reset passwords, and receives a forbidden response when attempting to do so.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Player_only_callers_cannot_enroll_players_or_reset_passwords()
    {
        using HttpClient apiClient = _fixture.CreateApiClient();
        TokenPair operatorTokenPair = await SignInAndGetTokenPairAsync(apiClient, AuthenticationAppFixture.OperatorPassword);

        using HttpRequestMessage enrollmentRequest = new(HttpMethod.Post, "/players")
        {
            Content = JsonContent.Create(new { Username = "PlayerOnly" }),
        };
        enrollmentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorTokenPair.AccessToken);
        using HttpResponseMessage enrollmentResponse = await apiClient.SendAsync(enrollmentRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, enrollmentResponse.StatusCode);
        using JsonDocument enrollmentBody = await JsonDocument.ParseAsync(
            await enrollmentResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        string? temporaryPlayerCredential = enrollmentBody.RootElement.GetProperty("temporaryCredential").GetString();
        Assert.NotNull(temporaryPlayerCredential);

        string restrictedPlayerAccessToken = await SignInAndGetAccessTokenAsync(
            apiClient,
            "PlayerOnly",
            temporaryPlayerCredential);
        await ChangePasswordAsync(
            apiClient,
            restrictedPlayerAccessToken,
            temporaryPlayerCredential,
            "Player-only-password1!");
        TokenPair playerTokenPair = await SignInAndGetTokenPairAsync(
            apiClient,
            "PlayerOnly",
            "Player-only-password1!");

        using HttpRequestMessage unauthorizedEnrollmentRequest = new(HttpMethod.Post, "/players")
        {
            Content = JsonContent.Create(new { Username = "UnauthorizedPlayer" }),
        };
        unauthorizedEnrollmentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", playerTokenPair.AccessToken);
        using HttpResponseMessage unauthorizedEnrollmentResponse = await apiClient.SendAsync(
            unauthorizedEnrollmentRequest,
            TestContext.Current.CancellationToken);

        using HttpRequestMessage unauthorizedResetRequest = new(
            HttpMethod.Post,
            "/players/FirstOperator/reset-password");
        unauthorizedResetRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", playerTokenPair.AccessToken);
        using HttpResponseMessage unauthorizedResetResponse = await apiClient.SendAsync(
            unauthorizedResetRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedEnrollmentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedResetResponse.StatusCode);
    }

    /// <summary>
    /// Tests that a player-only caller receives the correct immutable account ID when accessing their own player information, and that the account ID matches the subject claim in their access token.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Player_authorization_returns_the_normal_callers_immutable_account_id()
    {
        using HttpClient apiClient = _fixture.CreateApiClient();
        Uri meUri = new("/players/me", UriKind.Relative);
        using HttpResponseMessage anonymousResponse = await apiClient.GetAsync(
            meUri,
            TestContext.Current.CancellationToken);
        TokenPair operatorTokenPair = await SignInAndGetTokenPairAsync(apiClient, AuthenticationAppFixture.OperatorPassword);

        Uri enrollmentUri = new("/players", UriKind.Relative);
        using HttpRequestMessage enrollmentRequest = new(HttpMethod.Post, enrollmentUri)
        {
            Content = JsonContent.Create(new { Username = "IdentityPlayer" }),
        };
        enrollmentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorTokenPair.AccessToken);
        using HttpResponseMessage enrollmentResponse = await apiClient.SendAsync(enrollmentRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, enrollmentResponse.StatusCode);

        using JsonDocument enrollmentBody = await JsonDocument.ParseAsync(
            await enrollmentResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        string? temporaryCredential = enrollmentBody.RootElement.GetProperty("temporaryCredential").GetString();
        Assert.NotNull(temporaryCredential);

        string restrictedPlayerAccessToken = await SignInAndGetAccessTokenAsync(
            apiClient,
            "IdentityPlayer",
            temporaryCredential);
        using HttpRequestMessage restrictedRequest = new(HttpMethod.Get, meUri);
        restrictedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", restrictedPlayerAccessToken);
        using HttpResponseMessage restrictedResponse = await apiClient.SendAsync(
            restrictedRequest,
            TestContext.Current.CancellationToken);

        await ChangePasswordAsync(
            apiClient,
            restrictedPlayerAccessToken,
            temporaryCredential,
            "Identity-player-password1!");
        TokenPair playerTokenPair = await SignInAndGetTokenPairAsync(
            apiClient,
            "IdentityPlayer",
            "Identity-player-password1!");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, restrictedResponse.StatusCode);
        await AssertPlayerIdentityAsync(apiClient, operatorTokenPair.AccessToken);
        await AssertPlayerIdentityAsync(apiClient, playerTokenPair.AccessToken);
    }

    /// <summary>
    /// Tests that a player with a temporary credential can only replace their password before receiving a normal token pair, and that they cannot access restricted endpoints until they have replaced their password.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Temporary_credential_can_only_replace_its_password_before_receiving_a_normal_token_pair()
    {
        using HttpClient apiClient = _fixture.CreateApiClient();
        TokenPair operatorTokenPair = await SignInAndGetTokenPairAsync(apiClient, AuthenticationAppFixture.OperatorPassword);
        using HttpRequestMessage enrollmentRequest = new(HttpMethod.Post, "/players")
        {
            Content = JsonContent.Create(new { Username = "TemporaryCredentialPlayer" }),
        };
        enrollmentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorTokenPair.AccessToken);
        using HttpResponseMessage enrollmentResponse = await apiClient.SendAsync(enrollmentRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, enrollmentResponse.StatusCode);

        using JsonDocument enrollmentBody = await JsonDocument.ParseAsync(
            await enrollmentResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        string? temporaryCredential = enrollmentBody.RootElement.GetProperty("temporaryCredential").GetString();
        Assert.NotNull(temporaryCredential);

        DateTimeOffset signInStartedAt = DateTimeOffset.UtcNow;
        using HttpResponseMessage temporarySignInResponse = await apiClient.PostAsJsonAsync(
            "/authentication/sign-in",
            new
            {
                Username = "TEMPORARYCREDENTIALPLAYER",
                Password = temporaryCredential,
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, temporarySignInResponse.StatusCode);
        Assert.False(temporarySignInResponse.Headers.Contains("Set-Cookie"));

        using JsonDocument temporarySignInBody = await JsonDocument.ParseAsync(
            await temporarySignInResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(temporarySignInBody.RootElement.TryGetProperty("refreshToken", out _));
        Assert.False(temporarySignInBody.RootElement.TryGetProperty("refreshTokenExpiresAt", out _));
        string? restrictedAccessToken = temporarySignInBody.RootElement
            .GetProperty("accessToken")
            .GetString();
        DateTimeOffset restrictedAccessTokenExpiresAt = temporarySignInBody.RootElement
            .GetProperty("accessTokenExpiresAt")
            .GetDateTimeOffset();

        Assert.NotNull(restrictedAccessToken);
        AssertUsesRs256(restrictedAccessToken);
        Assert.InRange(
            restrictedAccessTokenExpiresAt,
            signInStartedAt.AddMinutes(4),
            signInStartedAt.AddMinutes(6));

        using HttpRequestMessage restrictedPlayerRequest = new(HttpMethod.Get, "/players/me");
        restrictedPlayerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", restrictedAccessToken);
        using HttpResponseMessage restrictedPlayerResponse = await apiClient.SendAsync(
            restrictedPlayerRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, restrictedPlayerResponse.StatusCode);

        using HttpRequestMessage passwordChangeRequest = new(HttpMethod.Post, "/authentication/change-password")
        {
            Content = JsonContent.Create(new
            {
                CurrentPassword = temporaryCredential,
                NewPassword = ReplacementPassword,
            }),
        };
        passwordChangeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", restrictedAccessToken);
        using HttpResponseMessage passwordChangeResponse = await apiClient.SendAsync(
            passwordChangeRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, passwordChangeResponse.StatusCode);
        Assert.False(passwordChangeResponse.Headers.Contains("Set-Cookie"));

        using JsonDocument passwordChangeBody = await JsonDocument.ParseAsync(
            await passwordChangeResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        string? normalAccessToken = passwordChangeBody.RootElement.GetProperty("accessToken").GetString();
        Assert.NotNull(normalAccessToken);
        Assert.True(passwordChangeBody.RootElement.TryGetProperty("refreshToken", out JsonElement refreshToken));
        Assert.NotNull(refreshToken.GetString());
        Assert.True(passwordChangeBody.RootElement.TryGetProperty("refreshTokenExpiresAt", out _));

        using HttpRequestMessage normalPlayerRequest = new(HttpMethod.Get, "/players/me");
        normalPlayerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", normalAccessToken);
        using HttpResponseMessage normalPlayerResponse = await apiClient.SendAsync(
            normalPlayerRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, normalPlayerResponse.StatusCode);
    }

    /// <summary>
    /// Tests that a normal sign-in returns a JSON token pair that can be used to authenticate a player request.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Normal_sign_in_returns_a_json_token_pair_that_authenticates_a_player_request()
    {
        using HttpClient apiClient = _fixture.CreateApiClient();

        using HttpResponseMessage normalSignInResponse = await apiClient.PostAsJsonAsync(
            "/authentication/sign-in",
            new
            {
                Username = BootstrapOperatorTestParameters.Username.ToUpperInvariant(),
                Password = ReplacementPassword,
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, normalSignInResponse.StatusCode);
        Assert.False(normalSignInResponse.Headers.Contains("Set-Cookie"));

        using JsonDocument normalSignInBody = await JsonDocument.ParseAsync(
            await normalSignInResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        string? accessToken = normalSignInBody.RootElement.GetProperty("accessToken").GetString();
        DateTimeOffset accessTokenExpiresAt = normalSignInBody.RootElement
            .GetProperty("accessTokenExpiresAt")
            .GetDateTimeOffset();
        Assert.NotNull(accessToken);
        AssertUsesRs256(accessToken);
        Assert.InRange(
            accessTokenExpiresAt,
            DateTimeOffset.UtcNow.AddMinutes(14),
            DateTimeOffset.UtcNow.AddMinutes(16));
        Assert.True(normalSignInBody.RootElement.TryGetProperty("accessTokenExpiresAt", out _));
        Assert.True(normalSignInBody.RootElement.TryGetProperty("refreshToken", out JsonElement refreshToken));
        Assert.NotNull(refreshToken.GetString());
        Assert.True(normalSignInBody.RootElement.TryGetProperty("refreshTokenExpiresAt", out _));

        using HttpRequestMessage playerRequest = new(HttpMethod.Get, "/players/me");
        playerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage playerResponse = await apiClient.SendAsync(playerRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, playerResponse.StatusCode);
    }

    /// <summary>
    /// Tests that refresh token rotation, replay detection, and sign-out are isolated to their respective sign-in session families, ensuring that revoking one session does not affect others.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Refresh_rotation_replay_and_sign_out_are_isolated_to_their_sign_in_session_family()
    {
        using HttpClient apiClient = _fixture.CreateApiClient();
        TokenPair firstFamily = await SignInAndGetTokenPairAsync(apiClient, ReplacementPassword);
        TokenPair secondFamily = await SignInAndGetTokenPairAsync(apiClient, ReplacementPassword);

        TokenPair firstFamilyReplacement = await RefreshAsync(apiClient, firstFamily.RefreshToken);
        Assert.NotEqual(firstFamily.RefreshToken, firstFamilyReplacement.RefreshToken);

        using HttpResponseMessage replayResponse = await RefreshRequestAsync(apiClient, firstFamily.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);

        using HttpResponseMessage revokedFamilyResponse = await RefreshRequestAsync(apiClient, firstFamilyReplacement.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedFamilyResponse.StatusCode);

        TokenPair secondFamilyReplacement = await RefreshAsync(apiClient, secondFamily.RefreshToken);

        using HttpResponseMessage signOutResponse = await apiClient.PostAsJsonAsync(
            "/authentication/sign-out",
            new { secondFamilyReplacement.RefreshToken },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, signOutResponse.StatusCode);

        using HttpResponseMessage signedOutFamilyResponse = await RefreshRequestAsync(apiClient, secondFamilyReplacement.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, signedOutFamilyResponse.StatusCode);

        using HttpRequestMessage activeAccessTokenRequest = new(HttpMethod.Get, "/players/me");
        activeAccessTokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secondFamily.AccessToken);
        using HttpResponseMessage activeAccessTokenResponse = await apiClient.SendAsync(
            activeAccessTokenRequest,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, activeAccessTokenResponse.StatusCode);
    }

    /// <summary>
    /// Tests that unknown usernames and invalid passwords return indistinguishable unauthorized responses, preventing attackers from determining valid usernames or password correctness.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Unknown_and_invalid_sign_ins_return_indistinguishable_unauthorized_responses()
    {
        using HttpClient apiClient = _fixture.CreateApiClient();
        using HttpResponseMessage unknownUsernameResponse = await SignInAsync(
            apiClient,
            "UnknownPlayer",
            BootstrapOperatorTestParameters.TemporaryPassword);
        using HttpResponseMessage invalidPasswordResponse = await SignInAsync(
            apiClient,
            BootstrapOperatorTestParameters.Username,
            "Incorrect-password1!");

        string unknownUsernameBody = await unknownUsernameResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        string invalidPasswordBody = await invalidPasswordResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, unknownUsernameResponse.StatusCode);
        Assert.Equal(unknownUsernameResponse.StatusCode, invalidPasswordResponse.StatusCode);
        Assert.Equal(unknownUsernameBody, invalidPasswordBody);
        Assert.False(unknownUsernameResponse.Headers.Contains("Set-Cookie"));
        Assert.False(invalidPasswordResponse.Headers.Contains("Set-Cookie"));
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores

    private static async Task AssertPlayerIdentityAsync(HttpClient apiClient, string accessToken)
    {
        using HttpRequestMessage playerRequest = new(HttpMethod.Get, "/players/me");
        playerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage playerResponse = await apiClient.SendAsync(
            playerRequest,
            TestContext.Current.CancellationToken).ConfigureAwait(false);

        Assert.Equal(HttpStatusCode.OK, playerResponse.StatusCode);

        using JsonDocument playerBody = await JsonDocument.ParseAsync(
            await playerResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken).ConfigureAwait(false), cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false);
        string? accountId = playerBody.RootElement.GetProperty("accountId").GetString();

        Assert.NotNull(accountId);
        Assert.True(Guid.TryParse(accountId, out _));
        Assert.Equal(ReadJwtSubject(accessToken), accountId);
        Assert.False(playerBody.RootElement.TryGetProperty("username", out _));
    }

    private static async Task<string> SignInAndGetAccessTokenAsync(
        HttpClient apiClient,
        string username,
        string password)
    {
        using HttpResponseMessage response = await apiClient.PostAsJsonAsync(
            "/authentication/sign-in",
            new
            {
                Username = username,
                Password = password,
            },
            TestContext.Current.CancellationToken).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken).ConfigureAwait(false), cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false);
        string? accessToken = body.RootElement.GetProperty("accessToken").GetString();
        Assert.NotNull(accessToken);
        return accessToken;
    }

    private static Task<HttpResponseMessage> SignInAsync(HttpClient apiClient, string username, string password) =>
        apiClient.PostAsJsonAsync(
            "/authentication/sign-in",
            new { Username = username, Password = password },
            TestContext.Current.CancellationToken);

    private static Task<TokenPair> SignInAndGetTokenPairAsync(HttpClient apiClient, string password) =>
        SignInAndGetTokenPairAsync(apiClient, BootstrapOperatorTestParameters.Username, password);

    private static async Task<TokenPair> SignInAndGetTokenPairAsync(
        HttpClient apiClient,
        string username,
        string password)
    {
        using HttpResponseMessage response = await SignInAsync(
            apiClient,
            username,
            password).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadTokenPairAsync(response).ConfigureAwait(false);
    }

    private static async Task<TokenPair> RefreshAsync(HttpClient apiClient, string refreshToken)
    {
        using HttpResponseMessage response = await RefreshRequestAsync(apiClient, refreshToken).ConfigureAwait(false);
        Assert.True(
            response.IsSuccessStatusCode,
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(false));

        return await ReadTokenPairAsync(response).ConfigureAwait(false);
    }

    private static Task<HttpResponseMessage> RefreshRequestAsync(HttpClient apiClient, string refreshToken) =>
        apiClient.PostAsJsonAsync(
            "/authentication/refresh",
            new { RefreshToken = refreshToken },
            TestContext.Current.CancellationToken);

    private static async Task<TokenPair> ReadTokenPairAsync(HttpResponseMessage response)
    {
        using JsonDocument body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken).ConfigureAwait(false), cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false);
        string? accessToken = body.RootElement.GetProperty("accessToken").GetString();
        string? refreshToken = body.RootElement.GetProperty("refreshToken").GetString();

        Assert.NotNull(accessToken);
        Assert.NotNull(refreshToken);
        return new TokenPair(accessToken, refreshToken);
    }

    private static void AssertUsesRs256(string accessToken)
    {
        string headerSegment = accessToken.Split('.')[0];
        string headerJson = Encoding.UTF8.GetString(
            System.Buffers.Text.Base64Url.DecodeFromChars(headerSegment));
        using JsonDocument header = JsonDocument.Parse(headerJson);

        Assert.Equal("RS256", header.RootElement.GetProperty("alg").GetString());
    }

    private static string ReadJwtSubject(string accessToken)
    {
        string payloadSegment = accessToken.Split('.')[1];
        string payloadJson = Encoding.UTF8.GetString(
            System.Buffers.Text.Base64Url.DecodeFromChars(payloadSegment));
        using JsonDocument payload = JsonDocument.Parse(payloadJson);

        return payload.RootElement.GetProperty("sub").GetString()!;
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
                CurrentPassword = currentPassword,
                NewPassword = newPassword,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage response = await apiClient.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record TokenPair(string AccessToken, string RefreshToken);
}