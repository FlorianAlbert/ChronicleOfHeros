using ChronicleOfHeros.Identity.AspNetCore.Identity;

using Microsoft.Extensions.Hosting;

namespace ChronicleOfHeros.Identity.AspNetCore;

internal sealed class BootstrapOperatorHostedService(IServiceProvider services) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        services.InitializeBootstrapOperatorAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}