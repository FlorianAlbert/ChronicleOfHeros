namespace ChronicleOfHeros.Api.Identity;

public sealed class BootstrapOperatorOptions
{
    public const string ConfigurationSectionName = "BootstrapOperator";

    public string? Username { get; set; }

    public string? TemporaryPassword { get; set; }
}