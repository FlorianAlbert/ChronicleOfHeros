using ChronicleOfHeros.Web.Client.Services.Abstractions.HealthReportService;

namespace ChronicleOfHeros.Web.Services.ServerHealthReportService;

internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddServerHealthReportService(string baseAddress)
        {
            services.AddHttpClient<IHealthReportService, ServerHealthReportService>(client => client.BaseAddress = new Uri(baseAddress));

            return services;
        }
    }
}