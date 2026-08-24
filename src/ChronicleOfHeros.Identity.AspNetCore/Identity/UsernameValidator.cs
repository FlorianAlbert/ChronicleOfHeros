using System.Text.RegularExpressions;

namespace ChronicleOfHeros.Identity.AspNetCore.Identity;

internal static partial class UsernameValidator
{
    public static bool IsValid(string? username) =>
        username is not null && UsernamePattern().IsMatch(username);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.-]{1,30}[A-Za-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}