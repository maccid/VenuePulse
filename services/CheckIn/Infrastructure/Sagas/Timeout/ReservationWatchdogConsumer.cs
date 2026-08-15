namespace VenuePulse.CheckIn.Infrastructure.Sagas.Timeout;

using MassTransit;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>Consume los eventos de reserva/pago de una entrada para mantener actualizado el deadline del watchdog.</summary>
public sealed class ReservationWatchdogConsumer(
    ReservationTimeoutTracker tracker,
    SagaTimeoutOptions options) :
    IConsumer<TicketPurchaseRequested>,
    IConsumer<ZoneSlotReserved>,
    IConsumer<ZoneSlotReservationFailed>,
    IConsumer<TicketPaymentSubmitted>
{
    public Task Consume(ConsumeContext<TicketPurchaseRequested> context)
    {
        tracker.Track(context.Message.CorrelationId, context.Message.ZoneId, DateTimeOffset.UtcNow + options.ReservationTimeout);
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<ZoneSlotReserved> context)
    {
        tracker.Track(context.Message.CorrelationId, context.Message.ZoneId, DateTimeOffset.UtcNow + options.ReservationTimeout);
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<ZoneSlotReservationFailed> context)
    {
        tracker.Untrack(context.Message.CorrelationId);
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<TicketPaymentSubmitted> context)
    {
        tracker.Untrack(context.Message.CorrelationId);
        return Task.CompletedTask;
    }
}
