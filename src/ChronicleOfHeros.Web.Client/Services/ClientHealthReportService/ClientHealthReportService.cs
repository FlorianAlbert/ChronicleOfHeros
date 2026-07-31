using ChronicleOfHeros.Web.Client.Services.Abstractions.HealthReportService;

namespace ChronicleOfHeros.Web.Client.Services.ClientHealthReportService;

internal class ClientHealthReportService : IHealthReportService
{
    private readonly HttpClient _httpClient;

    public ClientHealthReportService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
    public async Task<bool> IsApiHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}