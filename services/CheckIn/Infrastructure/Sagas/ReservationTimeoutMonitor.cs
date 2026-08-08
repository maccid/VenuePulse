namespace VenuePulse.CheckIn.Infrastructure.Sagas;

using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>
/// Sondea <see cref="ReservationTimeoutTracker"/> y publica <see cref="ReservationTimedOut"/>
/// para las reservas que superaron el deadline — ver ADR 0010 para por qué es polling en vez de
/// un <c>IMessageScheduler</c> de MassTransit.
/// </summary>
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
