namespace VenuePulse.CheckIn.Infrastructure.Sagas;

using MassTransit;
using Serilog;
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
                // El aforo ya está reservado de verdad aquí — el cliente pasa a elegir método de
                // pago (Tarjeta/Bizum en la UI), que puede tardar. El watchdog (ver
                // ReservationWatchdogConsumer) rearma su reloj para vigilar también esta espera.
                .TransitionTo(AwaitingPayment)
                // Publicado DESPUÉS de que MassTransit persista la transición — quien reacciona a
                // esto (TicketReadyForPaymentConsumer) tiene la garantía de que la Saga ya está de
                // verdad en AwaitingPayment, a diferencia de reaccionar a SlotReserved en paralelo.
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
            // AccessControl no respondió a tiempo (ver ADR 0010) — pero puede que sí haya llegado
            // a reservar el aforo de verdad y su respuesta solo vaya tarde. En vez de rechazar y
            // finalizar aquí mismo (lo que dejaba esa reserva tardía sin nadie que la compense,
            // ver ADR 0027), pasamos a un estado que todavía espera esa respuesta real para saber
            // si hace falta compensar o no.
            When(TimedOut)
                .Then(context =>
                {
                    context.Saga.RejectionReason = "ReservationTimedOut";
                    Logger.Warning("Timeout de reserva para la entrada {TicketId}", context.Saga.CorrelationId);
                })
                .TransitionTo(AwaitingLateReservationOutcome),
            // El evento se canceló (ver EventCancelledConsumer, ADR 0021) antes de que AccessControl
            // respondiera. El aforo nunca llegó a reservarse en este punto — igual que TimedOut aquí,
            // se rechaza sin publicar ReleaseZoneSlot.
            When(Cancelled)
                .Then(context =>
                {
                    context.Saga.RejectionReason = "EventCancelled";
                    Logger.Warning("Evento cancelado antes de reservar aforo (entrada {TicketId})", context.Saga.CorrelationId);
                })
                .Publish(context => new TicketPurchaseRejected(context.Saga.CorrelationId, "EventCancelled", context.Saga.GroupId))
                .TransitionTo(Rejected)
                .Finalize());

        // Cierra la ventana de carrera de ADR 0010: aquí la Saga ya se rindió de cara al watchdog,
        // pero todavía no sabemos si AccessControl SÍ llegó a reservar el aforo antes de que su
        // respuesta se retrasara. Riesgo residual aceptado (ver ADR 0027 y .ia/KNOWN-ISSUES.md): si
        // esa respuesta no solo tarda sino que se pierde del todo (AccessControl caído de verdad),
        // la Saga se queda esperando aquí sin un segundo timeout propio — mismo tipo de trade-off ya
        // aceptado en el resto del proyecto en vez de añadir un watchdog nuevo solo para este caso.
        During(AwaitingLateReservationOutcome,
            Ignore(Cancelled),
            // La reserva sí llegó a completarse en AccessControl — hay que compensarla antes de
            // rechazar definitivamente, no solo rechazar (igual que TimedOut en AwaitingPayment).
            When(SlotReserved)
                .Then(context => Logger.Warning("Reserva tardía completada tras el timeout — compensando la entrada {TicketId}", context.Saga.CorrelationId))
                .Publish(context => new ReleaseZoneSlot(context.Saga.CorrelationId, context.Saga.ZoneId))
                .TransitionTo(Confirming),
            // Confirma que de verdad nunca llegó a reservarse — nada que compensar.
            When(SlotReservationFailed)
                .Then(context => Logger.Information("Timeout de reserva sin reserva efectiva (entrada {TicketId})", context.Saga.CorrelationId))
                .Publish(context => new TicketPurchaseRejected(context.Saga.CorrelationId, context.Saga.RejectionReason!, context.Saga.GroupId))
                .TransitionTo(Rejected)
                .Finalize());

        During(AwaitingPayment,
            When(PaymentSubmitted)
                // Publicado ANTES del delay para que el timeline por SignalR muestre "Procesando
                // pago" como un paso real (ver ADR 0021), no una transición instantánea.
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
            // Aquí, a diferencia del timeout en Reserving, el aforo SÍ está reservado de verdad
            // (ver ADR sobre la partición de la Saga) — hace falta compensar, no solo rechazar.
            When(TimedOut)
                .Then(context =>
                {
                    context.Saga.RejectionReason = "PaymentTimedOut";
                    Logger.Warning("Timeout de pago para la entrada {TicketId}", context.Saga.CorrelationId);
                })
                .Publish(context => new ReleaseZoneSlot(context.Saga.CorrelationId, context.Saga.ZoneId))
                .TransitionTo(Confirming),
            // El evento se canceló mientras el cliente todavía no había elegido método de pago —
            // el aforo SÍ está reservado de verdad aquí, así que hace falta liberarlo (mismo camino
            // que TimedOut: Confirming resuelve el rechazo final al ver SlotReleased).
            When(Cancelled)
                .Then(context =>
                {
                    context.Saga.RejectionReason = "EventCancelled";
                    Logger.Warning("Evento cancelado con aforo ya reservado (entrada {TicketId})", context.Saga.CorrelationId);
                })
                .Publish(context => new ReleaseZoneSlot(context.Saga.CorrelationId, context.Saga.ZoneId))
                .TransitionTo(Confirming));

        During(Confirming,
            // Ventana de carrera aceptada (ver ADR 0021, mismo tipo de riesgo que ADR 0010): si
            // EventCancelled llega justo cuando la Saga ya pasó a Confirming (pago ya enviado), el
            // Ticket en Postgres puede seguir en "AwaitingPayment"/"ProcessingPayment" un instante y
            // la cascada de cancelación le publica un Cancelled que ya no tiene rama declarada aquí.
            // Se ignora en vez de dejar que MassTransit lance UnhandledEventException — el pago ya
            // en curso resuelve solo (Confirmado/Rechazado), sin relación con la cancelación.
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
