using Microsoft.Extensions.Configuration;

namespace ChronicleOfHeros.Identity.AspNetCore.Identity;

internal sealed class BootstrapOperatorOptions
{
    public const string ConfigurationSectionName = "BootstrapOperator";

    public string? Username { get; set; }

    public string? TemporaryPassword { get; set; }

    internal void ConfigureFrom(IConfiguration configuration)
    {
        var section = configuration.GetSection(ConfigurationSectionName);
        Username = section[nameof(Username)];
        TemporaryPassword = section[nameof(TemporaryPassword)];
    }
}