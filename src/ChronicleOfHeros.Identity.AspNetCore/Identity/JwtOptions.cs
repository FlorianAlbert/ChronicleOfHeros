using Microsoft.Extensions.Configuration;

namespace ChronicleOfHeros.Identity.AspNetCore.Identity;

internal sealed class JwtOptions
{
    public const string ConfigurationSectionName = "Jwt";

    public string? SigningPrivateKey { get; private set; }

    public string? Issuer { get; private set; }

    public string? Audience { get; private set; }

    internal void ConfigureFrom(IConfiguration configuration)
    {
        var section = configuration.GetSection(ConfigurationSectionName);
        SigningPrivateKey = section["SigningPrivateKey"];
        Issuer = section["Issuer"];
        Audience = section["Audience"];
    }

    internal bool HasRequiredValues() =>
        !string.IsNullOrWhiteSpace(SigningPrivateKey)
        && !string.IsNullOrWhiteSpace(Issuer)
        && !string.IsNullOrWhiteSpace(Audience);
}