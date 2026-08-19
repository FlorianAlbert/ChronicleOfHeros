using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using ChronicleOfHeros.Api.Data;
using ChronicleOfHeros.Api.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

[Collection("AppHost integration")]
public sealed class IdentityBootstrapTests
{
    [Fact]
    public async Task Startup_runs_migrations_and_bootstraps_one_operator_with_the_player_role()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(
                BootstrapOperatorTestParameters.CreateAppHostArguments(),
                TestContext.Current.CancellationToken);

        await using var app = await appHost.BuildAsync(TestContext.Current.CancellationToken);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotifications.WaitForResourceHealthyAsync("api", TestContext.Current.CancellationToken);

        var connectionString = await app.GetConnectionStringAsync(
            "chronicleofheros",
            TestContext.Current.CancellationToken);
        var dbContextOptions = new DbContextOptionsBuilder<ChronicleOfHerosDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var dbContext = new ChronicleOfHerosDbContext(dbContextOptions);

        var bootstrapOperator = await dbContext.Users.SingleOrDefaultAsync(
            user => user.UserName == BootstrapOperatorTestParameters.Username,
            TestContext.Current.CancellationToken);
        var persistedUsernames = await dbContext.Users
            .Select(user => user.UserName)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.True(
            bootstrapOperator is not null,
            $"Bootstrap Operator was not found. Persisted usernames: {string.Join(", ", persistedUsernames)}");
        var roleNames = await dbContext.UserRoles
            .Join(
                dbContext.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { UserRole = userRole, Role = role })
            .Where(joined => joined.UserRole.UserId == bootstrapOperator.Id)
            .OrderBy(joined => joined.Role.Name)
            .Select(joined => joined.Role.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.True(bootstrapOperator.IsActive);
        Assert.True(bootstrapOperator.MustChangePassword);
        Assert.NotEqual(BootstrapOperatorTestParameters.TemporaryPassword, bootstrapOperator.PasswordHash);
        Assert.Equal(["Operator", "Player"], roleNames);
        Assert.Empty(await dbContext.RefreshSessions.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Existing_operator_prevents_later_bootstrap_values_from_altering_accounts_or_roles()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ChronicleOfHeros_AppHost>(
                BootstrapOperatorTestParameters.CreateAppHostArguments(),
                TestContext.Current.CancellationToken);

        await using var app = await appHost.BuildAsync(TestContext.Current.CancellationToken);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotifications.WaitForResourceHealthyAsync("api", TestContext.Current.CancellationToken);

        var connectionString = await app.GetConnectionStringAsync(
            "chronicleofheros",
            TestContext.Current.CancellationToken);
        var dbContextOptions = new DbContextOptionsBuilder<ChronicleOfHerosDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var dbContext = new ChronicleOfHerosDbContext(dbContextOptions);
        var originalOperator = await dbContext.Users.SingleAsync(
            user => user.UserName == BootstrapOperatorTestParameters.Username,
            TestContext.Current.CancellationToken);
        var originalPasswordHash = originalOperator.PasswordHash;
        var originalRoleNames = await dbContext.UserRoles
            .Join(
                dbContext.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { UserRole = userRole, Role = role })
            .Where(joined => joined.UserRole.UserId == originalOperator.Id)
            .OrderBy(joined => joined.Role.Name)
            .Select(joined => joined.Role.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        var restartServices = new ServiceCollection();
        restartServices.AddDbContext<ChronicleOfHerosDbContext>(options => options.UseNpgsql(connectionString));
        restartServices.AddSingleton<IOptions<BootstrapOperatorOptions>>(
            Options.Create(new BootstrapOperatorOptions()));
        await using var restartedServiceProvider = restartServices.BuildServiceProvider();

        await restartedServiceProvider.InitializeBootstrapOperatorAsync(TestContext.Current.CancellationToken);

        dbContext.ChangeTracker.Clear();
        var persistedUsers = await dbContext.Users.ToListAsync(TestContext.Current.CancellationToken);
        var persistedOperator = await dbContext.Users.SingleAsync(
            user => user.Id == originalOperator.Id,
            TestContext.Current.CancellationToken);
        var persistedRoleNames = await dbContext.UserRoles
            .Join(
                dbContext.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { UserRole = userRole, Role = role })
            .Where(joined => joined.UserRole.UserId == persistedOperator.Id)
            .OrderBy(joined => joined.Role.Name)
            .Select(joined => joined.Role.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(persistedUsers);
        Assert.Equal(originalPasswordHash, persistedOperator.PasswordHash);
        Assert.Equal(originalRoleNames, persistedRoleNames);
    }
}