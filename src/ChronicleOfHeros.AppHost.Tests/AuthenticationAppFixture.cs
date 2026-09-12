using System.Net;
using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Microsoft.Extensions.DependencyInjection;

namespace ChronicleOfHeros.AppHost.Tests;

// xUnit requires public types for fixtures.
#pragma warning disable CA1515 // Consider making public types internal

/// <summary>
/// Starts one application instance for authentication endpoint scenarios.
/// </summary>
public sealed class AuthenticationAppFixture : IAsyncLifetime
{
    internal const string OperatorPassword = "Replacement-operator-password1!";

    private DistributedApplication? _application;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        IDistributedApplicationTestingBuilder appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(
                BootstrapOperatorTestParameters.CreateAppHostArguments()).ConfigureAwait(false);

        DistributedApplication application = await appHost.BuildAsync().ConfigureAwait(false);
        _application = application;
        await application.StartAsync().ConfigureAwait(false);

        ResourceNotificationService resourceNotifications = application.Services.GetRequiredService<ResourceNotificationService>();
        _ = await resourceNotifications.WaitForResourceHealthyAsync("api", CancellationToken.None).ConfigureAwait(false);

        using HttpClient apiClient = application.CreateHttpClient("api");
        string temporaryAccessToken = await SignInAndGetAccessTokenAsync(
            apiClient,
            BootstrapOperatorTestParameters.TemporaryPassword).ConfigureAwait(false);
        await ChangePasswordAsync(
            apiClient,
            temporaryAccessToken,
            BootstrapOperatorTestParameters.TemporaryPassword,
            OperatorPassword).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an API client for an individual test scenario.
    /// </summary>
    /// <returns>An API client connected to the shared application.</returns>
    public HttpClient CreateApiClient() => (_application ?? throw new InvalidOperationException("The application has not been started."))
        .CreateHttpClient("api");

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_application is not null)
        {
            await _application.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<string> SignInAndGetAccessTokenAsync(HttpClient apiClient, string password)
    {
        string username = BootstrapOperatorTestParameters.Username;
        using HttpResponseMessage response = await apiClient.PostAsJsonAsync(
            "/authentication/sign-in",
            new
            {
                username,
                password,
            },
            CancellationToken.None).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException("The bootstrap operator could not sign in.");
        }

        using JsonDocument body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync().ConfigureAwait(false),
            cancellationToken: CancellationToken.None).ConfigureAwait(false);
        return body.RootElement.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("The bootstrap operator sign-in did not return an access token.");
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
        request.Headers.Authorization = new("Bearer", accessToken);
        using HttpResponseMessage response = await apiClient.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException("The bootstrap operator password could not be changed.");
        }
    }
}

#pragma warning restore CA1515 // Consider making public types internal