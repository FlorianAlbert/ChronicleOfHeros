namespace ChronicleOfHeros.Web.Client.Services.Abstractions.HealthReportService;

public interface IHealthReportService
{
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    Task<bool> IsApiHealthyAsync(CancellationToken cancellationToken = default);
}
