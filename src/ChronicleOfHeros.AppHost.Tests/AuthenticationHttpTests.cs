using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;

namespace ChronicleOfHeros.AppHost.Tests;

/// <summary>
/// Integration tests for the authentication endpoints of the ChronicleOfHeros application host.
/// </summary>
[Collection("AppHost integration")]
public sealed class AuthenticationHttpTests
{
    private const string ReplacementPassword = "Replacement-operator-password1!";

    /// <summary>
    /// Tests that an operator can enroll a new player with a temporary credential that requires replacement, and that the player can sign in with the temporary credential and does not receive a refresh token.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Operator_can_enroll_a_player_with_a_temporary_credential_that_requires_replacement()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(
                CreateAppHostArguments(),
                TestContext.Current.CancellationToken);

        await using var app = await appHost.BuildAsync(TestContext.Current.CancellationToken);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotifications.WaitForResourceHealthyAsync("api", TestContext.Current.CancellationToken);

        using var apiClient = app.CreateHttpClient("api");
        var temporaryOperatorAccessToken = await SignInAndGetAccessTokenAsync(
            apiClient,
            BootstrapOperatorTestParameters.TemporaryPassword);
        await ChangePasswordAsync(
            apiClient,
            temporaryOperatorAccessToken,
            BootstrapOperatorTestParameters.TemporaryPassword,
            ReplacementPassword);
        var operatorTokenPair = await SignInAndGetTokenPairAsync(apiClient, ReplacementPassword);

        using var enrollmentRequest = new HttpRequestMessage(HttpMethod.Post, "/players")
        {
            Content = JsonContent.Create(new { Username = "EnrolledPlayer" }),
        };
        enrollmentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorTokenPair.AccessToken);
        using var enrollmentResponse = await apiClient.SendAsync(enrollmentRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, enrollmentResponse.StatusCode);
        Assert.Matches("^/players/[0-9a-f-]+$", enrollmentResponse.Headers.Location?.OriginalString);
        Assert.False(enrollmentResponse.Headers.Contains("Set-Cookie"));

        using var enrollmentBody = JsonDocument.Parse(
            await enrollmentResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        var temporaryCredential = enrollmentBody.RootElement.GetProperty("temporaryCredential").GetString();
        Assert.NotNull(temporaryCredential);

        using var playerSignInResponse = await SignInAsync(apiClient, "enrolledplayer", temporaryCredential);
        Assert.Equal(HttpStatusCode.OK, playerSignInResponse.StatusCode);
        using var playerSignInBody = JsonDocument.Parse(
            await playerSignInResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        Assert.False(playerSignInBody.RootElement.TryGetProperty("refreshToken", out _));
    }
    
    /// <summary>
    /// Tests that when an operator resets a player's password, a new temporary credential is issued and all of the player's existing refresh sessions are revoked.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Operator_password_reset_issues_a_temporary_credential_and_revokes_the_players_refresh_sessions()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(
                CreateAppHostArguments(),
                TestContext.Current.CancellationToken);

        await using var app = await appHost.BuildAsync(TestContext.Current.CancellationToken);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotifications.WaitForResourceHealthyAsync("api", TestContext.Current.CancellationToken);

        using var apiClient = app.CreateHttpClient("api");
        var temporaryOperatorAccessToken = await SignInAndGetAccessTokenAsync(
            apiClient,
            BootstrapOperatorTestParameters.TemporaryPassword);
        await ChangePasswordAsync(
            apiClient,
            temporaryOperatorAccessToken,
            BootstrapOperatorTestParameters.TemporaryPassword,
            ReplacementPassword);
        var operatorTokenPair = await SignInAndGetTokenPairAsync(apiClient, ReplacementPassword);

        using var enrollmentRequest = new HttpRequestMessage(HttpMethod.Post, "/players")
        {
            Content = JsonContent.Create(new { Username = "ResettablePlayer" }),
        };
        enrollmentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorTokenPair.AccessToken);
        using var enrollmentResponse = await apiClient.SendAsync(enrollmentRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, enrollmentResponse.StatusCode);
        using var enrollmentBody = JsonDocument.Parse(
            await enrollmentResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        var initialCredential = enrollmentBody.RootElement.GetProperty("temporaryCredential").GetString();
        Assert.NotNull(initialCredential);

        var restrictedPlayerAccessToken = await SignInAndGetAccessTokenAsync(
            apiClient,
            "ResettablePlayer",
            initialCredential);
        await ChangePasswordAsync(
            apiClient,
            restrictedPlayerAccessToken,
            initialCredential,
            "Original-player-password1!");
        var playerTokenPair = await SignInAndGetTokenPairAsync(
            apiClient,
            "ResettablePlayer",
            "Original-player-password1!");
        var secondPlayerTokenPair = await SignInAndGetTokenPairAsync(
            apiClient,
            "ResettablePlayer",
            "Original-player-password1!");

        using var resetRequest = new HttpRequestMessage(HttpMethod.Post, "/players/ResettablePlayer/reset-password");
        resetRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorTokenPair.AccessToken);
        using var resetResponse = await apiClient.SendAsync(resetRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        Assert.False(resetResponse.Headers.Contains("Set-Cookie"));
        using var resetBody = JsonDocument.Parse(
            await resetResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        var resetCredential = resetBody.RootElement.GetProperty("temporaryCredential").GetString();
        Assert.NotNull(resetCredential);
        Assert.NotEqual(initialCredential, resetCredential);

        using var revokedRefreshResponse = await RefreshRequestAsync(apiClient, playerTokenPair.RefreshToken);
        using var secondRevokedRefreshResponse = await RefreshRequestAsync(
            apiClient,
            secondPlayerTokenPair.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedRefreshResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, secondRevokedRefreshResponse.StatusCode);

        using var resetCredentialSignInResponse = await SignInAsync(apiClient, "resettableplayer", resetCredential);
        Assert.Equal(HttpStatusCode.OK, resetCredentialSignInResponse.StatusCode);
        using var resetCredentialSignInBody = JsonDocument.Parse(
            await resetCredentialSignInResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        Assert.False(resetCredentialSignInBody.RootElement.TryGetProperty("refreshToken", out _));
    }
    
    /// <summary>
    /// Tests that a player-only caller cannot enroll new players or reset passwords, and receives a forbidden response when attempting to do so.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Player_only_callers_cannot_enroll_players_or_reset_passwords()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(
                CreateAppHostArguments(),
                TestContext.Current.CancellationToken);

        await using var app = await appHost.BuildAsync(TestContext.Current.CancellationToken);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotifications.WaitForResourceHealthyAsync("api", TestContext.Current.CancellationToken);

        using var apiClient = app.CreateHttpClient("api");
        var temporaryOperatorAccessToken = await SignInAndGetAccessTokenAsync(
            apiClient,
            BootstrapOperatorTestParameters.TemporaryPassword);
        await ChangePasswordAsync(
            apiClient,
            temporaryOperatorAccessToken,
            BootstrapOperatorTestParameters.TemporaryPassword,
            ReplacementPassword);
        var operatorTokenPair = await SignInAndGetTokenPairAsync(apiClient, ReplacementPassword);

        using var enrollmentRequest = new HttpRequestMessage(HttpMethod.Post, "/players")
        {
            Content = JsonContent.Create(new { Username = "PlayerOnly" }),
        };
        enrollmentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorTokenPair.AccessToken);
        using var enrollmentResponse = await apiClient.SendAsync(enrollmentRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, enrollmentResponse.StatusCode);
        using var enrollmentBody = JsonDocument.Parse(
            await enrollmentResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        var temporaryPlayerCredential = enrollmentBody.RootElement.GetProperty("temporaryCredential").GetString();
        Assert.NotNull(temporaryPlayerCredential);

        var restrictedPlayerAccessToken = await SignInAndGetAccessTokenAsync(
            apiClient,
            "PlayerOnly",
            temporaryPlayerCredential);
        await ChangePasswordAsync(
            apiClient,
            restrictedPlayerAccessToken,
            temporaryPlayerCredential,
            "Player-only-password1!");
        var playerTokenPair = await SignInAndGetTokenPairAsync(
            apiClient,
            "PlayerOnly",
            "Player-only-password1!");

        using var unauthorizedEnrollmentRequest = new HttpRequestMessage(HttpMethod.Post, "/players")
        {
            Content = JsonContent.Create(new { Username = "UnauthorizedPlayer" }),
        };
        unauthorizedEnrollmentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", playerTokenPair.AccessToken);
        using var unauthorizedEnrollmentResponse = await apiClient.SendAsync(
            unauthorizedEnrollmentRequest,
            TestContext.Current.CancellationToken);

        using var unauthorizedResetRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/players/FirstOperator/reset-password");
        unauthorizedResetRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", playerTokenPair.AccessToken);
        using var unauthorizedResetResponse = await apiClient.SendAsync(
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
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(
                CreateAppHostArguments(),
                TestContext.Current.CancellationToken);

        await using var app = await appHost.BuildAsync(TestContext.Current.CancellationToken);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotifications.WaitForResourceHealthyAsync("api", TestContext.Current.CancellationToken);

        using var apiClient = app.CreateHttpClient("api");
        using var anonymousResponse = await apiClient.GetAsync(
            "/players/me",
            TestContext.Current.CancellationToken);

        var restrictedAccessToken = await SignInAndGetAccessTokenAsync(
            apiClient,
            BootstrapOperatorTestParameters.TemporaryPassword);
        using var restrictedRequest = new HttpRequestMessage(HttpMethod.Get, "/players/me");
        restrictedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", restrictedAccessToken);
        using var restrictedResponse = await apiClient.SendAsync(
            restrictedRequest,
            TestContext.Current.CancellationToken);

        await ChangePasswordAsync(
            apiClient,
            restrictedAccessToken,
            BootstrapOperatorTestParameters.TemporaryPassword,
            ReplacementPassword);
        var operatorTokenPair = await SignInAndGetTokenPairAsync(apiClient, ReplacementPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, restrictedResponse.StatusCode);
        await AssertPlayerIdentityAsync(apiClient, operatorTokenPair.AccessToken);

        using var enrollmentRequest = new HttpRequestMessage(HttpMethod.Post, "/players")
        {
            Content = JsonContent.Create(new { Username = "IdentityPlayer" }),
        };
        enrollmentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorTokenPair.AccessToken);
        using var enrollmentResponse = await apiClient.SendAsync(enrollmentRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, enrollmentResponse.StatusCode);

        using var enrollmentBody = JsonDocument.Parse(
            await enrollmentResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        var temporaryCredential = enrollmentBody.RootElement.GetProperty("temporaryCredential").GetString();
        Assert.NotNull(temporaryCredential);

        var restrictedPlayerAccessToken = await SignInAndGetAccessTokenAsync(
            apiClient,
            "IdentityPlayer",
            temporaryCredential);
        await ChangePasswordAsync(
            apiClient,
            restrictedPlayerAccessToken,
            temporaryCredential,
            "Identity-player-password1!");
        var playerTokenPair = await SignInAndGetTokenPairAsync(
            apiClient,
            "IdentityPlayer",
            "Identity-player-password1!");

        await AssertPlayerIdentityAsync(apiClient, playerTokenPair.AccessToken);
    }

    private static async Task AssertPlayerIdentityAsync(HttpClient apiClient, string accessToken)
    {
        using var playerRequest = new HttpRequestMessage(HttpMethod.Get, "/players/me");
        playerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var playerResponse = await apiClient.SendAsync(
            playerRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, playerResponse.StatusCode);

        using var playerBody = JsonDocument.Parse(
            await playerResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        var accountId = playerBody.RootElement.GetProperty("accountId").GetString();

        Assert.NotNull(accountId);
        Assert.True(Guid.TryParse(accountId, out _));
        Assert.Equal(ReadJwtSubject(accessToken), accountId);
        Assert.False(playerBody.RootElement.TryGetProperty("username", out _));
    }
    
    /// <summary>
    /// Tests that a player with a temporary credential can only replace their password before receiving a normal token pair, and that they cannot access restricted endpoints until they have replaced their password.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Temporary_credential_can_only_replace_its_password_before_receiving_a_normal_token_pair()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(
                CreateAppHostArguments(),
                TestContext.Current.CancellationToken);

        await using var app = await appHost.BuildAsync(TestContext.Current.CancellationToken);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotifications.WaitForResourceHealthyAsync("api", TestContext.Current.CancellationToken);

        using var apiClient = app.CreateHttpClient("api");
        var signInStartedAt = DateTimeOffset.UtcNow;
        using var temporarySignInResponse = await apiClient.PostAsJsonAsync(
            "/authentication/sign-in",
            new
            {
                Username = BootstrapOperatorTestParameters.Username.ToLowerInvariant(),
                Password = BootstrapOperatorTestParameters.TemporaryPassword,
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, temporarySignInResponse.StatusCode);
        Assert.False(temporarySignInResponse.Headers.Contains("Set-Cookie"));

        using var temporarySignInBody = JsonDocument.Parse(
            await temporarySignInResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        Assert.False(temporarySignInBody.RootElement.TryGetProperty("refreshToken", out _));
        Assert.False(temporarySignInBody.RootElement.TryGetProperty("refreshTokenExpiresAt", out _));
        var restrictedAccessToken = temporarySignInBody.RootElement
            .GetProperty("accessToken")
            .GetString();
        var restrictedAccessTokenExpiresAt = temporarySignInBody.RootElement
            .GetProperty("accessTokenExpiresAt")
            .GetDateTimeOffset();

        Assert.NotNull(restrictedAccessToken);
        AssertUsesRs256(restrictedAccessToken);
        Assert.InRange(
            restrictedAccessTokenExpiresAt,
            signInStartedAt.AddMinutes(4),
            signInStartedAt.AddMinutes(6));

        using var restrictedPlayerRequest = new HttpRequestMessage(HttpMethod.Get, "/players/me");
        restrictedPlayerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", restrictedAccessToken);
        using var restrictedPlayerResponse = await apiClient.SendAsync(
            restrictedPlayerRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, restrictedPlayerResponse.StatusCode);

        using var passwordChangeRequest = new HttpRequestMessage(HttpMethod.Post, "/authentication/change-password")
        {
            Content = JsonContent.Create(new
            {
                CurrentPassword = BootstrapOperatorTestParameters.TemporaryPassword,
                NewPassword = ReplacementPassword,
            }),
        };
        passwordChangeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", restrictedAccessToken);
        using var passwordChangeResponse = await apiClient.SendAsync(
            passwordChangeRequest,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, passwordChangeResponse.StatusCode);
        Assert.False(passwordChangeResponse.Headers.Contains("Set-Cookie"));

        using var passwordChangeBody = JsonDocument.Parse(
            await passwordChangeResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        var normalAccessToken = passwordChangeBody.RootElement.GetProperty("accessToken").GetString();
        Assert.NotNull(normalAccessToken);
        Assert.True(passwordChangeBody.RootElement.TryGetProperty("refreshToken", out var refreshToken));
        Assert.NotNull(refreshToken.GetString());
        Assert.True(passwordChangeBody.RootElement.TryGetProperty("refreshTokenExpiresAt", out _));

        using var normalPlayerRequest = new HttpRequestMessage(HttpMethod.Get, "/players/me");
        normalPlayerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", normalAccessToken);
        using var normalPlayerResponse = await apiClient.SendAsync(
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
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(
                CreateAppHostArguments(),
                TestContext.Current.CancellationToken);

        await using var app = await appHost.BuildAsync(TestContext.Current.CancellationToken);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotifications.WaitForResourceHealthyAsync("api", TestContext.Current.CancellationToken);

        using var apiClient = app.CreateHttpClient("api");
        var temporaryAccessToken = await SignInAndGetAccessTokenAsync(
            apiClient,
            BootstrapOperatorTestParameters.TemporaryPassword);
        await ChangePasswordAsync(
            apiClient,
            temporaryAccessToken,
            BootstrapOperatorTestParameters.TemporaryPassword,
            ReplacementPassword);

        using var normalSignInResponse = await apiClient.PostAsJsonAsync(
            "/authentication/sign-in",
            new
            {
                Username = BootstrapOperatorTestParameters.Username.ToLowerInvariant(),
                Password = ReplacementPassword,
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, normalSignInResponse.StatusCode);
        Assert.False(normalSignInResponse.Headers.Contains("Set-Cookie"));

        using var normalSignInBody = JsonDocument.Parse(
            await normalSignInResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        var accessToken = normalSignInBody.RootElement.GetProperty("accessToken").GetString();
        var accessTokenExpiresAt = normalSignInBody.RootElement
            .GetProperty("accessTokenExpiresAt")
            .GetDateTimeOffset();
        Assert.NotNull(accessToken);
        AssertUsesRs256(accessToken);
        Assert.InRange(
            accessTokenExpiresAt,
            DateTimeOffset.UtcNow.AddMinutes(14),
            DateTimeOffset.UtcNow.AddMinutes(16));
        Assert.True(normalSignInBody.RootElement.TryGetProperty("accessTokenExpiresAt", out _));
        Assert.True(normalSignInBody.RootElement.TryGetProperty("refreshToken", out var refreshToken));
        Assert.NotNull(refreshToken.GetString());
        Assert.True(normalSignInBody.RootElement.TryGetProperty("refreshTokenExpiresAt", out _));

        using var playerRequest = new HttpRequestMessage(HttpMethod.Get, "/players/me");
        playerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var playerResponse = await apiClient.SendAsync(playerRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, playerResponse.StatusCode);
    }
    
    /// <summary>
    /// Tests that refresh token rotation, replay detection, and sign-out are isolated to their respective sign-in session families, ensuring that revoking one session does not affect others.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Refresh_rotation_replay_and_sign_out_are_isolated_to_their_sign_in_session_family()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(
                CreateAppHostArguments(),
                TestContext.Current.CancellationToken);

        await using var app = await appHost.BuildAsync(TestContext.Current.CancellationToken);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotifications.WaitForResourceHealthyAsync("api", TestContext.Current.CancellationToken);

        using var apiClient = app.CreateHttpClient("api");
        var temporaryAccessToken = await SignInAndGetAccessTokenAsync(
            apiClient,
            BootstrapOperatorTestParameters.TemporaryPassword);
        await ChangePasswordAsync(
            apiClient,
            temporaryAccessToken,
            BootstrapOperatorTestParameters.TemporaryPassword,
            ReplacementPassword);

        var firstFamily = await SignInAndGetTokenPairAsync(apiClient, ReplacementPassword);
        var secondFamily = await SignInAndGetTokenPairAsync(apiClient, ReplacementPassword);

        var firstFamilyReplacement = await RefreshAsync(apiClient, firstFamily.RefreshToken);
        Assert.NotEqual(firstFamily.RefreshToken, firstFamilyReplacement.RefreshToken);

        using var replayResponse = await RefreshRequestAsync(apiClient, firstFamily.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);

        using var revokedFamilyResponse = await RefreshRequestAsync(apiClient, firstFamilyReplacement.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedFamilyResponse.StatusCode);

        var secondFamilyReplacement = await RefreshAsync(apiClient, secondFamily.RefreshToken);

        using var signOutResponse = await apiClient.PostAsJsonAsync(
            "/authentication/sign-out",
            new { RefreshToken = secondFamilyReplacement.RefreshToken },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, signOutResponse.StatusCode);

        using var signedOutFamilyResponse = await RefreshRequestAsync(apiClient, secondFamilyReplacement.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, signedOutFamilyResponse.StatusCode);

        using var activeAccessTokenRequest = new HttpRequestMessage(HttpMethod.Get, "/players/me");
        activeAccessTokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secondFamily.AccessToken);
        using var activeAccessTokenResponse = await apiClient.SendAsync(
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
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(
                CreateAppHostArguments(),
                TestContext.Current.CancellationToken);

        await using var app = await appHost.BuildAsync(TestContext.Current.CancellationToken);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotifications.WaitForResourceHealthyAsync("api", TestContext.Current.CancellationToken);

        using var apiClient = app.CreateHttpClient("api");
        using var unknownUsernameResponse = await SignInAsync(
            apiClient,
            "UnknownPlayer",
            BootstrapOperatorTestParameters.TemporaryPassword);
        using var invalidPasswordResponse = await SignInAsync(
            apiClient,
            BootstrapOperatorTestParameters.Username,
            "Incorrect-password1!");

        var unknownUsernameBody = await unknownUsernameResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var invalidPasswordBody = await invalidPasswordResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, unknownUsernameResponse.StatusCode);
        Assert.Equal(unknownUsernameResponse.StatusCode, invalidPasswordResponse.StatusCode);
        Assert.Equal(unknownUsernameBody, invalidPasswordBody);
        Assert.False(unknownUsernameResponse.Headers.Contains("Set-Cookie"));
        Assert.False(invalidPasswordResponse.Headers.Contains("Set-Cookie"));
    }

    private static Task<string> SignInAndGetAccessTokenAsync(HttpClient apiClient, string password) =>
        SignInAndGetAccessTokenAsync(apiClient, BootstrapOperatorTestParameters.Username, password);

    private static async Task<string> SignInAndGetAccessTokenAsync(
        HttpClient apiClient,
        string username,
        string password)
    {
        using var response = await apiClient.PostAsJsonAsync(
            "/authentication/sign-in",
            new
            {
                Username = username,
                Password = password,
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        var accessToken = body.RootElement.GetProperty("accessToken").GetString();
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
        using var response = await SignInAsync(
            apiClient,
            username,
            password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadTokenPairAsync(response);
    }

    private static async Task<TokenPair> RefreshAsync(HttpClient apiClient, string refreshToken)
    {
        using var response = await RefreshRequestAsync(apiClient, refreshToken);
        Assert.True(
            response.IsSuccessStatusCode,
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return await ReadTokenPairAsync(response);
    }

    private static Task<HttpResponseMessage> RefreshRequestAsync(HttpClient apiClient, string refreshToken) =>
        apiClient.PostAsJsonAsync(
            "/authentication/refresh",
            new { RefreshToken = refreshToken },
            TestContext.Current.CancellationToken);

    private static async Task<TokenPair> ReadTokenPairAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        var accessToken = body.RootElement.GetProperty("accessToken").GetString();
        var refreshToken = body.RootElement.GetProperty("refreshToken").GetString();

        Assert.NotNull(accessToken);
        Assert.NotNull(refreshToken);
        return new TokenPair(accessToken, refreshToken);
    }

    private static void AssertUsesRs256(string accessToken)
    {
        var headerSegment = accessToken.Split('.')[0];
        var headerJson = Encoding.UTF8.GetString(
            System.Buffers.Text.Base64Url.DecodeFromChars(headerSegment));
        using var header = JsonDocument.Parse(headerJson);

        Assert.Equal("RS256", header.RootElement.GetProperty("alg").GetString());
    }

    private static string ReadJwtSubject(string accessToken)
    {
        var payloadSegment = accessToken.Split('.')[1];
        var payloadJson = Encoding.UTF8.GetString(
            System.Buffers.Text.Base64Url.DecodeFromChars(payloadSegment));
        using var payload = JsonDocument.Parse(payloadJson);

        return payload.RootElement.GetProperty("sub").GetString()!;
    }

    private static async Task ChangePasswordAsync(
        HttpClient apiClient,
        string accessToken,
        string currentPassword,
        string newPassword)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/authentication/change-password")
        {
            Content = JsonContent.Create(new
            {
                CurrentPassword = currentPassword,
                NewPassword = newPassword,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await apiClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string[] CreateAppHostArguments() => BootstrapOperatorTestParameters.CreateAppHostArguments();

    private sealed record TokenPair(string AccessToken, string RefreshToken);
}