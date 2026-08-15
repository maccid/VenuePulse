namespace VenuePulse.CheckIn.Infrastructure.Sagas.Timeout;

using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VenuePulse.Core.Messaging.Contracts;

public sealed class ReservationTimeoutMonitor(
    ReservationTimeoutTracker tracker,
    IBus bus,
    SagaTimeoutOptions options,
    ILogger<ReservationTimeoutMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var (correlationId, zoneId) in tracker.SweepExpired(DateTimeOffset.UtcNow))
            {
                logger.LogWarning("Reserva {CorrelationId} de la zona {ZoneId} superó el timeout de {Timeout}", correlationId, zoneId, options.ReservationTimeout);
                await bus.Publish(new ReservationTimedOut(correlationId, zoneId), stoppingToken);
            }
        }
    }
}
