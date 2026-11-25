namespace ChronicleOfHeros.Web.Endpoints;

internal static class LocalReturnPath
{
    internal static string GetSafe(string? returnUrl) =>
        IsSafe(returnUrl) ? returnUrl! : "/";

    private static bool IsSafe(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)
            || returnUrl.Contains('\\', StringComparison.Ordinal)
            || !Uri.TryCreate(returnUrl, UriKind.Relative, out _))
        {
            return false;
        }

        int pathEnd = returnUrl.IndexOfAny(['?', '#']);
        string encodedPath = pathEnd < 0 ? returnUrl : returnUrl[..pathEnd];
        string decodedPath = encodedPath;

        while (true)
        {
            string nextPath = Uri.UnescapeDataString(decodedPath);
            if (nextPath == decodedPath)
            {
                break;
            }

            decodedPath = nextPath;
        }

        return decodedPath[0] == '/'
               && !decodedPath.StartsWith("//", StringComparison.Ordinal)
               && !decodedPath.StartsWith("/\\", StringComparison.Ordinal)
               && !decodedPath.Contains('\\', StringComparison.Ordinal);
    }
}
