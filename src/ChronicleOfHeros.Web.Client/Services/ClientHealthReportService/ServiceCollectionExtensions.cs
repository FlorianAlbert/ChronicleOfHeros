using ChronicleOfHeros.Web.Client.Services.Abstractions.HealthReportService;

namespace ChronicleOfHeros.Web.Client.Services.ClientHealthReportService;

internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddClientHealthReportService(string baseAddress)
        {
            services.AddHttpClient<IHealthReportService, ClientHealthReportService>(client => client.BaseAddress = new Uri(baseAddress));

            return services;
        }
    }
}