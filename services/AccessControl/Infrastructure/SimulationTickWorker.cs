namespace VenuePulse.AccessControl.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VenuePulse.AccessControl.Application;

/// <summary>
/// Corazón del motor de simulación (Bloque 8): cada tick resuelve los servicios scoped desde un
/// scope nuevo (el motor es singleton, las zonas y los snapshots no) y avanza los runs activos.
/// Solo trabaja cuando hay al menos un run en marcha — parado no cuesta nada.
/// </summary>
public sealed class SimulationTickWorker(
    IServiceScopeFactory scopeFactory,
    ISimulationEngine engine,
    ILogger<SimulationTickWorker> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var zoneService = scope.ServiceProvider.GetRequiredService<IZoneService>();
                var snapshotRepository = scope.ServiceProvider.GetRequiredService<IOccupancySnapshotRepository>();
                await engine.AdvanceAsync(DateTimeOffset.UtcNow, zoneService, snapshotRepository, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Simulation tick failed");
            }
        }
    }
}
