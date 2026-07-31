using ChronicleOfHeros.Web.Client.Services.Abstractions.HealthReportService;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ChronicleOfHeros.Web.Services.ServerHealthReportService;

internal class ServerHealthReportService : IHealthReportService
{
    private readonly HealthCheckService _healthCheckService;
    private readonly HttpClient _apiHttpClient;

    public ServerHealthReportService(HealthCheckService healthCheckService, HttpClient apiHttpClient)
    {
        _healthCheckService = healthCheckService;
        _apiHttpClient = apiHttpClient;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        HealthReport healthReport = await _healthCheckService.CheckHealthAsync(cancellationToken);
        return healthReport.Status == HealthStatus.Healthy;
    }
    public async Task<bool> IsApiHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiHttpClient.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}