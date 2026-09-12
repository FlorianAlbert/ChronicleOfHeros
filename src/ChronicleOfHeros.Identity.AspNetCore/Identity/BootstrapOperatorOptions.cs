using Microsoft.Extensions.Configuration;

namespace ChronicleOfHeros.Identity.AspNetCore.Identity;

// This class gets used by the dependency injection system
#pragma warning disable CA1812 // Avoid uninstantiated internal classes

internal sealed class BootstrapOperatorOptions
{
    public const string ConfigurationSectionName = "BootstrapOperator";

    public string? Username { get; set; }

    public string? TemporaryPassword { get; set; }

    internal void ConfigureFrom(IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(ConfigurationSectionName);
        Username = section[nameof(Username)];
        TemporaryPassword = section[nameof(TemporaryPassword)];
    }
}

#pragma warning restore CA1812 // Avoid uninstantiated internal classes