using AzmCrm.Application.Features.Automation.Commands.ScanSlaBreaches;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzmCrm.Infrastructure.Sla;

/// <summary>
/// Polls for overdue tickets on a fixed interval and escalates them via
/// <see cref="ScanSlaBreachesCommand"/>. Runs in its own DI scope per tick, since
/// <c>IApplicationDbContext</c>/<c>IMediator</c> are scoped services and this service itself
/// is a long-lived singleton (ASP.NET Core's <see cref="BackgroundService"/> contract).
/// </summary>
internal sealed class SlaMonitoringBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<SlaMonitoringSettings> settings,
    ILogger<SlaMonitoringBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, settings.Value.IntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var result = await mediator.Send(new ScanSlaBreachesCommand(), stoppingToken);

                if (result.IsSuccess && result.Data > 0)
                    logger.LogInformation("SLA monitoring scan escalated {Count} ticket(s).", result.Data);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed scan must not crash the host or stop future ticks — the next
                // PeriodicTimer tick retries automatically.
                logger.LogError(ex, "SLA monitoring scan failed.");
            }
        }
    }
}
