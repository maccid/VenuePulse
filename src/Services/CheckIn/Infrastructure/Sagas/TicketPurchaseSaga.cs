namespace VenuePulse.CheckIn.Infrastructure.Sagas;

using MassTransit;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>
/// Orquestador de la compra de una entrada. Pieza central de la demo: modela el flujo
/// reservar aforo -> "pago" mockeado -> confirmar/compensar, con una compensación real
/// (ReleaseZoneSlot) cuando algo falla tras un paso ya completado — ver ADR 0001 y
/// docs/demo-script.md para el camino de compensación en vivo.
/// </summary>
public sealed class TicketPurchaseSaga : MassTransitStateMachine<TicketPurchaseSagaState>
{
    public State Reserving { get; private set; } = null!;
    public State Confirming { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Rejected { get; private set; } = null!;

    public Event<TicketPurchaseRequested> PurchaseRequested { get; private set; } = null!;
    public Event<ZoneSlotReserved> SlotReserved { get; private set; } = null!;
    public Event<ZoneSlotReservationFailed> SlotReservationFailed { get; private set; } = null!;
    public Event<ZoneSlotConfirmed> SlotConfirmed { get; private set; } = null!;
    public Event<ZoneSlotReleased> SlotReleased { get; private set; } = null!;

    public TicketPurchaseSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => PurchaseRequested, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => SlotReserved, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => SlotReservationFailed, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => SlotConfirmed, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => SlotReleased, x => x.CorrelateById(m => m.Message.CorrelationId));

        Initially(
            When(PurchaseRequested)
                .Then(context =>
                {
                    context.Saga.ZoneId = context.Message.ZoneId;
                    context.Saga.SimulateFailure = context.Message.SimulateFailure;
                    context.Saga.RequestedAtUtc = context.Message.OccurredOnUtc;
                })
                .Publish(context => new ReserveZoneSlot(context.Saga.CorrelationId, context.Saga.ZoneId, context.Saga.SimulateFailure))
                .TransitionTo(Reserving));

        During(Reserving,
            When(SlotReserved)
                .ThenAsync(async context =>
                {
                    context.Saga.SlotReservedAtUtc = context.Message.OccurredOnUtc;

                    // Se lee del mensaje recién recibido (eco del ReserveZoneSlot original), no
                    // del estado releído de la Saga — ver ADR sobre esta decisión de diseño.
                    if (context.Message.SimulateFailure)
                        await context.Publish(new ReleaseZoneSlot(context.Saga.CorrelationId, context.Saga.ZoneId));
                    else
                        await context.Publish(new ConfirmZoneSlot(context.Saga.CorrelationId, context.Saga.ZoneId));
                })
                .TransitionTo(Confirming),
            When(SlotReservationFailed)
                .Then(context => context.Saga.RejectionReason = context.Message.Reason)
                .Publish(context => new TicketPurchaseRejected(context.Saga.CorrelationId, context.Message.Reason))
                .TransitionTo(Rejected)
                .Finalize());

        During(Confirming,
            When(SlotConfirmed)
                .Publish(context => new TicketPurchaseConfirmed(context.Saga.CorrelationId))
                .TransitionTo(Completed)
                .Finalize(),
            When(SlotReleased)
                .Then(context => context.Saga.RejectionReason ??= "PaymentFailed")
                .Publish(context => new TicketPurchaseRejected(context.Saga.CorrelationId, context.Saga.RejectionReason!))
                .TransitionTo(Rejected)
                .Finalize());

        SetCompletedWhenFinalized();
    }
}
