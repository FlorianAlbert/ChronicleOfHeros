using Microsoft.Extensions.Configuration;

namespace ChronicleOfHeros.Identity.AspNetCore.Infrastructure.Identity;

// This class gets used by the dependency injection system
#pragma warning disable CA1812 // Avoid uninstantiated internal classes

internal sealed class JwtOptions
{
    public const string ConfigurationSectionName = "Jwt";

    public string? SigningPrivateKey { get; private set; }

    public string? Issuer { get; private set; }

    public string? Audience { get; private set; }

    internal void ConfigureFrom(IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(ConfigurationSectionName);
        SigningPrivateKey = section["SigningPrivateKey"];
        Issuer = section["Issuer"];
        Audience = section["Audience"];
    }

    internal bool HasRequiredValues() =>
        !string.IsNullOrWhiteSpace(SigningPrivateKey)
        && !string.IsNullOrWhiteSpace(Issuer)
        && !string.IsNullOrWhiteSpace(Audience);
}

#pragma warning restore CA1812 // Avoid uninstantiated internal classes
