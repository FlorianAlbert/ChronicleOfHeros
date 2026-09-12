using ChronicleOfHeros.Identity.AspNetCore.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChronicleOfHeros.Identity.AspNetCore;

// This class gets used by the dependency injection system 
// and may not be directly instantiated.
#pragma warning disable CA1812 // Avoid uninstantiated internal classes

internal sealed class IdentityMigrationHostedService(
    IServiceProvider services,
    IHostApplicationLifetime lifetime) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            ChronicleOfHerosDbContext dbContext = scope.ServiceProvider.GetRequiredService<ChronicleOfHerosDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            lifetime.StopApplication();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

#pragma warning restore CA1812 // Avoid uninstantiated internal classes
