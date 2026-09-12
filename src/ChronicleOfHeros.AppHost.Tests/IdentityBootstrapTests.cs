using System.Net;
using System.Security.Cryptography;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Microsoft.Extensions.DependencyInjection;

namespace ChronicleOfHeros.AppHost.Tests;

internal static class BootstrapOperatorTestParameters
{
    internal const string Username = "FirstOperator";
    internal const string TemporaryPassword = "First-operator-temporary-password1!";

    internal static string[] CreateAppHostArguments()
    {
        using RSA rsa = RSA.Create(2048);

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

#pragma warning disable CA1707 // Identifiers should not contain underscores

    /// <summary>
    /// Verifies that the ChronicleOfHeros.AppHost project runs migrations and bootstraps an operator with a temporary credential, allowing sign-in via the API.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task Startup_runs_migrations_and_bootstraps_an_operator_with_a_temporary_credential()
    {
        IDistributedApplicationTestingBuilder appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(
                BootstrapOperatorTestParameters.CreateAppHostArguments(),
                TestContext.Current.CancellationToken);

        DistributedApplication distributedApplication = await appHost.BuildAsync(TestContext.Current.CancellationToken);
        await using (distributedApplication.ConfigureAwait(false))
        {
            await distributedApplication.StartAsync(TestContext.Current.CancellationToken);

            ResourceNotificationService resourceNotifications = distributedApplication.Services.GetRequiredService<ResourceNotificationService>();
            _ = await resourceNotifications.WaitForResourceHealthyAsync("api", TestContext.Current.CancellationToken);

            using HttpClient apiClient = distributedApplication.CreateHttpClient("api");
            using HttpResponseMessage signInResponse = await apiClient.PostAsJsonAsync(
                "/authentication/sign-in",
                new
                {
                    BootstrapOperatorTestParameters.Username,
                    Password = BootstrapOperatorTestParameters.TemporaryPassword,
                },
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);
        }
    }

#pragma warning restore CA1707 // Identifiers should not contain underscores

}