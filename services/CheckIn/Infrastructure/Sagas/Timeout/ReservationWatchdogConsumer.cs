namespace VenuePulse.CheckIn.Infrastructure.Sagas.Timeout;

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
        // El aforo ya está reservado — rearma el reloj para vigilar la siguiente espera real (que
        // el cliente elija método de pago), en vez de dejar de vigilar del todo.
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
