namespace ChronicleOfHeros.Web.Endpoints;

internal static class AuthenticationEndpointExtensions
{
    extension(WebApplication webApplication)
    {
        public WebApplication MapAuthenticationEndpoints()
        {
            AuthenticationEndpoint.Map(webApplication);
            return webApplication;
        }
    }
}
