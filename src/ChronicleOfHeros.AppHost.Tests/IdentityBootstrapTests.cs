using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace ChronicleOfHeros.AppHost.Tests;

internal static class BootstrapOperatorTestParameters
{
    internal const string Username = "FirstOperator";
    internal const string TemporaryPassword = "First-operator-temporary-password1!";

    internal static string[] CreateAppHostArguments()
    {
        using var rsa = RSA.Create(2048);

        return
        [
            $"Parameters:bootstrap-operator-username={Username}",
            $"Parameters:bootstrap-operator-temporary-password={TemporaryPassword}",
            $"Parameters:jwt-signing-private-key={Convert.ToBase64String(rsa.ExportPkcs8PrivateKey())}",
            "Parameters:jwt-issuer=https://identity.chronicleofheros.test",
            "Parameters:jwt-audience=chronicleofheros-api-tests",
        ];
    }
}

/// <summary>
/// Integration tests for the ChronicleOfHeros.AppHost project that verify the bootstrap operator is created and can sign in with a temporary credential.
/// </summary>
[Collection("AppHost integration")]
public sealed class IdentityBootstrapTests
{
    /// <summary>
    /// Verifies that the ChronicleOfHeros.AppHost project runs migrations and bootstraps an operator with a temporary credential, allowing sign-in via the API.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Startup_runs_migrations_and_bootstraps_an_operator_with_a_temporary_credential()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(
                BootstrapOperatorTestParameters.CreateAppHostArguments(),
                TestContext.Current.CancellationToken);

        await using var app = await appHost.BuildAsync(TestContext.Current.CancellationToken);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotifications.WaitForResourceHealthyAsync("api", TestContext.Current.CancellationToken);

        using var apiClient = app.CreateHttpClient("api");
        using var signInResponse = await apiClient.PostAsJsonAsync(
            "/authentication/sign-in",
            new
            {
                Username = BootstrapOperatorTestParameters.Username,
                Password = BootstrapOperatorTestParameters.TemporaryPassword,
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);
    }
}