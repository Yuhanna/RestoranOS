using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RestaurantOS.Infrastructure;

/// <summary>
/// Warns once at startup when live-panel leases fall back to in-memory outside Development.
/// Multi-instance deployments need ConnectionStrings:SignalRRedis.
/// </summary>
internal sealed partial class LivePanelInMemoryLeaseWarningHostedService(
    ILogger<LivePanelInMemoryLeaseWarningHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        LogInMemoryLease(logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventId = 5101,
        Level = LogLevel.Warning,
        Message = "Live-panel session lease uses in-memory storage because ConnectionStrings:SignalRRedis is not set. This is unsafe across multiple API instances; configure SignalRRedis for production scale-out.")]
    private static partial void LogInMemoryLease(ILogger logger);
}
