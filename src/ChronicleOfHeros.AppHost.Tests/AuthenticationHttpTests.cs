using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using ChronicleOfHeros.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;

namespace ChronicleOfHeros.AppHost.Tests;

[Collection("AppHost integration")]
public sealed class AuthenticationHttpTests
{
    private const string ReplacementPassword = "Replacement-operator-password1!";

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

    [Fact]
    public async Task Unknown_invalid_and_disabled_sign_ins_return_indistinguishable_unauthorized_responses()
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

        var connectionString = await app.GetConnectionStringAsync(
            "chronicleofheros",
            TestContext.Current.CancellationToken);
        var dbContextOptions = new DbContextOptionsBuilder<ChronicleOfHerosDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using (var dbContext = new ChronicleOfHerosDbContext(dbContextOptions))
        {
            var bootstrapOperator = await dbContext.Users.SingleAsync(
                user => user.UserName == BootstrapOperatorTestParameters.Username,
                TestContext.Current.CancellationToken);
            bootstrapOperator.IsActive = false;
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var disabledAccountResponse = await SignInAsync(
            apiClient,
            BootstrapOperatorTestParameters.Username,
            BootstrapOperatorTestParameters.TemporaryPassword);

        var unknownUsernameBody = await unknownUsernameResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var invalidPasswordBody = await invalidPasswordResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var disabledAccountBody = await disabledAccountResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, unknownUsernameResponse.StatusCode);
        Assert.Equal(unknownUsernameResponse.StatusCode, invalidPasswordResponse.StatusCode);
        Assert.Equal(unknownUsernameResponse.StatusCode, disabledAccountResponse.StatusCode);
        Assert.Equal(unknownUsernameBody, invalidPasswordBody);
        Assert.Equal(unknownUsernameBody, disabledAccountBody);
        Assert.False(unknownUsernameResponse.Headers.Contains("Set-Cookie"));
        Assert.False(invalidPasswordResponse.Headers.Contains("Set-Cookie"));
        Assert.False(disabledAccountResponse.Headers.Contains("Set-Cookie"));
    }

    private static async Task<string> SignInAndGetAccessTokenAsync(HttpClient apiClient, string password)
    {
        using var response = await apiClient.PostAsJsonAsync(
            "/authentication/sign-in",
            new
            {
                Username = BootstrapOperatorTestParameters.Username,
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

    private static void AssertUsesRs256(string accessToken)
    {
        var headerSegment = accessToken.Split('.')[0];
        var headerJson = Encoding.UTF8.GetString(
            System.Buffers.Text.Base64Url.DecodeFromChars(headerSegment));
        using var header = JsonDocument.Parse(headerJson);

        Assert.Equal("RS256", header.RootElement.GetProperty("alg").GetString());
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
}