using Microsoft.AspNetCore.Antiforgery;

namespace ChronicleOfHeros.Web.Endpoints;

internal static class DisplayLanguageEndpointExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddDisplayLanguageEndpoint(string[] supportedCultures)
        {
            _ = builder.Services.AddScoped(services => new DisplayLanguageEndpoint(
                services.GetRequiredService<IAntiforgery>(),
                supportedCultures));

            return builder;
        }
    }

    extension(WebApplication webApplication)
    {
        public WebApplication MapDisplayLanguageEndpoint()
        {
            DisplayLanguageEndpoint.Map(webApplication);
            return webApplication;
        }
    }
}
