namespace VenuePulse.CheckIn.Infrastructure.Sagas;

using MassTransit;
using Serilog;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>Orquesta la compra de una entrada: reserva de aforo -> pago -> confirmación o compensación.</summary>
public sealed class TicketPurchaseSaga : MassTransitStateMachine<TicketPurchaseSagaState>
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<TicketPurchaseSaga>();

    public State Reserving { get; private set; } = null!;
    public State AwaitingLateReservationOutcome { get; private set; } = null!;
    public State AwaitingPayment { get; private set; } = null!;
    public State Confirming { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Rejected { get; private set; } = null!;

    public Event<TicketPurchaseRequested> PurchaseRequested { get; private set; } = null!;
    public Event<ZoneSlotReserved> SlotReserved { get; private set; } = null!;
    public Event<ZoneSlotReservationFailed> SlotReservationFailed { get; private set; } = null!;
    public Event<TicketPaymentSubmitted> PaymentSubmitted { get; private set; } = null!;
    public Event<ZoneSlotConfirmed> SlotConfirmed { get; private set; } = null!;
    public Event<ZoneSlotReleased> SlotReleased { get; private set; } = null!;
    public Event<ReservationTimedOut> TimedOut { get; private set; } = null!;
    public Event<TicketPurchaseCancelled> Cancelled { get; private set; } = null!;

    public TicketPurchaseSaga(PaymentProcessingOptions paymentOptions)
    {
        InstanceState(x => x.CurrentState);

        Event(() => PurchaseRequested, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => SlotReserved, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => SlotReservationFailed, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => PaymentSubmitted, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => SlotConfirmed, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => SlotReleased, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => TimedOut, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => Cancelled, x => x.CorrelateById(m => m.Message.CorrelationId));

        Initially(
            When(PurchaseRequested)
                .Then(context =>
                {
                    context.Saga.ZoneId = context.Message.ZoneId;
                    context.Saga.GroupId = context.Message.GroupId;
                    context.Saga.RequestedAtUtc = context.Message.OccurredOnUtc;
                    Logger.Information("Compra solicitada para la entrada {TicketId} (zona {ZoneId})", context.Saga.CorrelationId, context.Saga.ZoneId);
                })
                .Publish(context => new ReserveZoneSlot(context.Saga.CorrelationId, context.Saga.ZoneId))
                .TransitionTo(Reserving));

        During(Reserving,
            When(SlotReserved)
                .Then(context =>
                {
                    context.Saga.SlotReservedAtUtc = context.Message.OccurredOnUtc;
                    Logger.Information("Aforo reservado para la entrada {TicketId}", context.Saga.CorrelationId);
                })
                .TransitionTo(AwaitingPayment)
                .Publish(context => new TicketAwaitingPayment(context.Saga.CorrelationId, context.Saga.GroupId)),
            When(SlotReservationFailed)
                .Then(context =>
                {
                    context.Saga.RejectionReason = context.Message.Reason;
                    Logger.Warning("Reserva de aforo fallida para la entrada {TicketId}: {Reason}", context.Saga.CorrelationId, context.Message.Reason);
                })
                .Publish(context => new TicketPurchaseRejected(context.Saga.CorrelationId, context.Message.Reason, context.Saga.GroupId))
                .TransitionTo(Rejected)
                .Finalize(),
            When(TimedOut)
                .Then(context =>
                {
                    context.Saga.RejectionReason = "ReservationTimedOut";
                    Logger.Warning("Timeout de reserva para la entrada {TicketId}", context.Saga.CorrelationId);
                })
                .TransitionTo(AwaitingLateReservationOutcome),
            When(Cancelled)
                .Then(context =>
                {
                    context.Saga.RejectionReason = context.Message.Reason;
                    Logger.Warning("Entrada cancelada antes de reservar aforo (entrada {TicketId}): {Reason}", context.Saga.CorrelationId, context.Message.Reason);
                })
                .Publish(context => new TicketPurchaseRejected(context.Saga.CorrelationId, context.Message.Reason, context.Saga.GroupId))
                .TransitionTo(Rejected)
                .Finalize());

        During(AwaitingLateReservationOutcome,
            Ignore(Cancelled),
            When(SlotReserved)
                .Then(context => Logger.Warning("Reserva tardía completada tras el timeout — compensando la entrada {TicketId}", context.Saga.CorrelationId))
                .Publish(context => new ReleaseZoneSlot(context.Saga.CorrelationId, context.Saga.ZoneId))
                .TransitionTo(Confirming),
            When(SlotReservationFailed)
                .Then(context => Logger.Information("Timeout de reserva sin reserva efectiva (entrada {TicketId})", context.Saga.CorrelationId))
                .Publish(context => new TicketPurchaseRejected(context.Saga.CorrelationId, context.Saga.RejectionReason!, context.Saga.GroupId))
                .TransitionTo(Rejected)
                .Finalize());

        During(AwaitingPayment,
            When(PaymentSubmitted)
                .Publish(context => new TicketProcessingPayment(context.Saga.CorrelationId))
                .ThenAsync(async context =>
                {
                    Logger.Information(context.Message.SimulateFailure
                        ? "Pago recibido con fallo SIMULADO para la entrada {TicketId}"
                        : "Pago recibido para la entrada {TicketId}", context.Saga.CorrelationId);
                    await Task.Delay(paymentOptions.Delay, context.CancellationToken);

                    if (context.Message.SimulateFailure)
                        await context.Publish(new ReleaseZoneSlot(context.Saga.CorrelationId, context.Saga.ZoneId));
                    else
                        await context.Publish(new ConfirmZoneSlot(context.Saga.CorrelationId, context.Saga.ZoneId));
                })
                .TransitionTo(Confirming),
            When(TimedOut)
                .Then(context =>
                {
                    context.Saga.RejectionReason = "PaymentTimedOut";
                    Logger.Warning("Timeout de pago para la entrada {TicketId}", context.Saga.CorrelationId);
                })
                .Publish(context => new ReleaseZoneSlot(context.Saga.CorrelationId, context.Saga.ZoneId))
                .TransitionTo(Confirming),
            When(Cancelled)
                .Then(context =>
                {
                    context.Saga.RejectionReason = context.Message.Reason;
                    Logger.Warning("Entrada cancelada con aforo ya reservado (entrada {TicketId}): {Reason}", context.Saga.CorrelationId, context.Message.Reason);
                })
                .Publish(context => new ReleaseZoneSlot(context.Saga.CorrelationId, context.Saga.ZoneId))
                .TransitionTo(Confirming));

        During(Confirming,
            Ignore(Cancelled),
            When(SlotConfirmed)
                .Then(context => Logger.Information("Compra confirmada para la entrada {TicketId}", context.Saga.CorrelationId))
                .Publish(context => new TicketPurchaseConfirmed(context.Saga.CorrelationId, context.Saga.GroupId))
                .TransitionTo(Completed)
                .Finalize(),
            When(SlotReleased)
                .Then(context =>
                {
                    context.Saga.RejectionReason ??= "PaymentFailed";
                    Logger.Warning("Compra rechazada para la entrada {TicketId}: {Reason}", context.Saga.CorrelationId, context.Saga.RejectionReason);
                })
                .Publish(context => new TicketPurchaseRejected(context.Saga.CorrelationId, context.Saga.RejectionReason!, context.Saga.GroupId))
                .TransitionTo(Rejected)
                .Finalize());

        SetCompletedWhenFinalized();
    }
}
