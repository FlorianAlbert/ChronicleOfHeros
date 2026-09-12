using ChronicleOfHeros.Identity.AspNetCore.Identity;

using Microsoft.Extensions.Hosting;

namespace ChronicleOfHeros.Identity.AspNetCore;

// This class gets used by the dependency injection system 
// and may not be directly instantiated.
#pragma warning disable CA1812 // Avoid uninstantiated internal classes

internal sealed class BootstrapOperatorHostedService(IServiceProvider services) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        services.InitializeBootstrapOperatorAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

#pragma warning restore CA1812 // Avoid uninstantiated internal classes
