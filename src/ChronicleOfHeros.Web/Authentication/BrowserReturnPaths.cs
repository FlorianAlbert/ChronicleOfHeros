namespace ChronicleOfHeros.Web.Authentication;

internal static class BrowserReturnPaths
{
    internal static string GetSafeLocalReturnPath(string? returnUrl) =>
        IsSafeLocalReturnPath(returnUrl) ? returnUrl! : "/";

    internal static string GetSafeSignInReturnPath(string? returnUrl)
    {
        string safeReturnPath = GetSafeLocalReturnPath(returnUrl);
        return string.Equals(GetDecodedPath(safeReturnPath), "/sign-in", StringComparison.OrdinalIgnoreCase)
            ? "/"
            : safeReturnPath;
    }

    private static bool IsSafeLocalReturnPath(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)
            || returnUrl.Contains('\\', StringComparison.Ordinal)
            || !Uri.TryCreate(returnUrl, UriKind.Relative, out _))
        {
            return false;
        }

        string decodedPath = GetDecodedPath(returnUrl);
        return decodedPath[0] == '/'
               && !decodedPath.StartsWith("//", StringComparison.Ordinal)
               && !decodedPath.StartsWith("/\\", StringComparison.Ordinal)
               && !decodedPath.Contains('\\', StringComparison.Ordinal);
    }

    private static string GetDecodedPath(string returnUrl)
    {
        int pathEnd = returnUrl.IndexOfAny(['?', '#']);
        string decodedPath = pathEnd < 0 ? returnUrl : returnUrl[..pathEnd];

        while (true)
        {
            string nextPath = Uri.UnescapeDataString(decodedPath);
            if (nextPath == decodedPath)
            {
                return decodedPath;
            }

            decodedPath = nextPath;
        }
    }
}
