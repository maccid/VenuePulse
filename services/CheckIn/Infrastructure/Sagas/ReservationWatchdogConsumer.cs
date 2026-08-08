namespace VenuePulse.CheckIn.Infrastructure.Sagas;

using MassTransit;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>
/// Alimenta el <see cref="ReservationTimeoutTracker"/> observando los mismos eventos que la
/// Saga, sin acoplarse a su estado interno — ver ADR 0010.
/// </summary>
public sealed class ReservationWatchdogConsumer(
    ReservationTimeoutTracker tracker,
    SagaTimeoutOptions options) :
    IConsumer<TicketPurchaseRequested>,
    IConsumer<ZoneSlotReserved>,
    IConsumer<ZoneSlotReservationFailed>
{
    public Task Consume(ConsumeContext<TicketPurchaseRequested> context)
    {
        tracker.Track(context.Message.CorrelationId, context.Message.ZoneId, DateTimeOffset.UtcNow + options.ReservationTimeout);
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<ZoneSlotReserved> context)
    {
        tracker.Untrack(context.Message.CorrelationId);
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<ZoneSlotReservationFailed> context)
    {
        tracker.Untrack(context.Message.CorrelationId);
        return Task.CompletedTask;
    }
}
