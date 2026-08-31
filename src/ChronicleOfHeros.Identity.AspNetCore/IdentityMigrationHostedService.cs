using ChronicleOfHeros.Identity.AspNetCore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChronicleOfHeros.Identity.AspNetCore;

internal sealed class IdentityMigrationHostedService(
    IServiceProvider services,
    IHostApplicationLifetime lifetime) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ChronicleOfHerosDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            lifetime.StopApplication();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}