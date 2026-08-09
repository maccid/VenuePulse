namespace VenuePulse.CheckIn.Infrastructure.Sagas;

using MassTransit;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>
/// Orquestador de la compra de una entrada. Pieza central de la demo: modela el flujo
/// reservar aforo -> esperar a que el cliente elija método de pago -> "pago" mockeado ->
/// confirmar/compensar, con una compensación real (ReleaseZoneSlot) cuando algo falla tras un
/// paso ya completado — ver ADR 0001, ADR 0010 y docs/demo-script.md para el camino de
/// compensación en vivo.
/// </summary>
public sealed class TicketPurchaseSaga : MassTransitStateMachine<TicketPurchaseSagaState>
{
    public State Reserving { get; private set; } = null!;
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

    public TicketPurchaseSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => PurchaseRequested, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => SlotReserved, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => SlotReservationFailed, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => PaymentSubmitted, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => SlotConfirmed, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => SlotReleased, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => TimedOut, x => x.CorrelateById(m => m.Message.CorrelationId));

        Initially(
            When(PurchaseRequested)
                .Then(context =>
                {
                    context.Saga.ZoneId = context.Message.ZoneId;
                    context.Saga.RequestedAtUtc = context.Message.OccurredOnUtc;
                })
                .Publish(context => new ReserveZoneSlot(context.Saga.CorrelationId, context.Saga.ZoneId))
                .TransitionTo(Reserving));

        During(Reserving,
            When(SlotReserved)
                .Then(context => context.Saga.SlotReservedAtUtc = context.Message.OccurredOnUtc)
                // El aforo ya está reservado de verdad aquí — el cliente pasa a elegir método de
                // pago (Tarjeta/Bizum en la UI), que puede tardar. El watchdog (ver
                // ReservationWatchdogConsumer) rearma su reloj para vigilar también esta espera.
                .TransitionTo(AwaitingPayment)
                // Publicado DESPUÉS de que MassTransit persista la transición — quien reacciona a
                // esto (TicketReadyForPaymentConsumer) tiene la garantía de que la Saga ya está de
                // verdad en AwaitingPayment, a diferencia de reaccionar a SlotReserved en paralelo.
                .Publish(context => new TicketAwaitingPayment(context.Saga.CorrelationId)),
            When(SlotReservationFailed)
                .Then(context => context.Saga.RejectionReason = context.Message.Reason)
                .Publish(context => new TicketPurchaseRejected(context.Saga.CorrelationId, context.Message.Reason))
                .TransitionTo(Rejected)
                .Finalize(),
            // AccessControl no respondió a tiempo (ver ADR 0010) — se rechaza en vez de dejar la
            // Saga colgada en Reserving para siempre. Riesgo aceptado: si SlotReserved llega
            // justo después de este timeout, la Saga ya está finalizada y ese aforo reservado no
            // se libera solo (mismo tipo de trade-off ya aceptado en KNOWN-ISSUES.md). Sin
            // ReleaseZoneSlot aquí porque el aforo nunca llegó a reservarse en este punto.
            When(TimedOut)
                .Then(context => context.Saga.RejectionReason = "ReservationTimedOut")
                .Publish(context => new TicketPurchaseRejected(context.Saga.CorrelationId, "ReservationTimedOut"))
                .TransitionTo(Rejected)
                .Finalize());

        During(AwaitingPayment,
            When(PaymentSubmitted)
                .ThenAsync(async context =>
                {
                    if (context.Message.SimulateFailure)
                        await context.Publish(new ReleaseZoneSlot(context.Saga.CorrelationId, context.Saga.ZoneId));
                    else
                        await context.Publish(new ConfirmZoneSlot(context.Saga.CorrelationId, context.Saga.ZoneId));
                })
                .TransitionTo(Confirming),
            // Aquí, a diferencia del timeout en Reserving, el aforo SÍ está reservado de verdad
            // (ver ADR sobre la partición de la Saga) — hace falta compensar, no solo rechazar.
            When(TimedOut)
                .Then(context => context.Saga.RejectionReason = "PaymentTimedOut")
                .Publish(context => new ReleaseZoneSlot(context.Saga.CorrelationId, context.Saga.ZoneId))
                .TransitionTo(Confirming));

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
