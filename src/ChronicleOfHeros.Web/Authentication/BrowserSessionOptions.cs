namespace ChronicleOfHeros.Web.Authentication;

// This class gets used by the dependency injection system.
#pragma warning disable CA1812 // Avoid uninstantiated internal classes

internal sealed class BrowserSessionOptions
{
    internal const string ConfigurationSectionName = "Jwt";

    internal string? SigningPublicKey { get; private set; }

    internal string? Issuer { get; private set; }

    internal string? Audience { get; private set; }

    internal void ConfigureFrom(IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(ConfigurationSectionName);
        SigningPublicKey = section["SigningPublicKey"];
        Issuer = section["Issuer"];
        Audience = section["Audience"];
    }

    internal bool HasRequiredValues() =>
        !string.IsNullOrWhiteSpace(SigningPublicKey)
        && !string.IsNullOrWhiteSpace(Issuer)
        && !string.IsNullOrWhiteSpace(Audience);
}

#pragma warning restore CA1812 // Avoid uninstantiated internal classes
