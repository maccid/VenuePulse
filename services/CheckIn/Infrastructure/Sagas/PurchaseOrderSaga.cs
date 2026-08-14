namespace VenuePulse.CheckIn.Infrastructure.Sagas;

using MassTransit;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>
/// Orquestador de un pedido multi-entrada (ADR 0028) POR ENCIMA de N <see cref="TicketPurchaseSaga"/>
/// aisladas, una por cada línea — nunca toca su lógica interna, solo observa los mismos eventos que
/// ya publica (<see cref="TicketAwaitingPayment"/>/<see cref="TicketPurchaseConfirmed"/>/
/// <see cref="TicketPurchaseRejected"/>) correlacionados por <c>GroupId</c> en vez de por su
/// <c>CorrelationId</c> nativo (que identifica al <c>Ticket</c>, no al pedido) — ver
/// <see cref="PurchaseOrderSagaState"/>. Garantiza todo-o-nada: si cualquier línea falla al
/// reservar, cancela las hermanas (<see cref="TicketPurchaseCancelled"/>, ya manejado por
/// <see cref="TicketPurchaseSaga"/> sin mecanismo de compensación nuevo); si cualquier línea falla
/// al pagar, compensa (<see cref="ReleaseConfirmedZoneSlot"/>+<see cref="TicketRefunded"/>) las
/// hermanas ya confirmadas — mismos mensajes que ya usa la cascada de cancelación de evento (ADR
/// 0020/0021), sin watchdog/mensaje de compensación nuevo (ver ADR 0028, "Alternativas descartadas").
/// </summary>
public sealed class PurchaseOrderSaga : MassTransitStateMachine<PurchaseOrderSagaState>
{
    public State ReservingLines { get; private set; } = null!;
    public State AwaitingPayment { get; private set; } = null!;
    public State ConfirmingLines { get; private set; } = null!;
    public State CancellingLines { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Rejected { get; private set; } = null!;

    public Event<PurchaseOrderRequested> OrderRequested { get; private set; } = null!;
    public Event<TicketAwaitingPayment> TicketReady { get; private set; } = null!;
    public Event<TicketPurchaseConfirmed> TicketConfirmed { get; private set; } = null!;
    public Event<TicketPurchaseRejected> TicketRejected { get; private set; } = null!;
    public Event<PurchaseOrderPaymentSubmitted> OrderPaymentSubmitted { get; private set; } = null!;

    public PurchaseOrderSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderRequested, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => OrderPaymentSubmitted, x => x.CorrelateById(m => m.Message.CorrelationId));

        // Estos 3 eventos los publica la propia TicketPurchaseSaga de cada línea hermana con su
        // CorrelationId nativo = Ticket.Id (no el pedido) — hay que correlacionar por GroupId, el
        // campo de propagación añadido a esos mensajes (ver ADR 0028). Si GroupId es null (compra
        // suelta, pre-ADR 0028, sin pedido), Guid.Empty nunca coincide con ningún CorrelationId de
        // pedido real (siempre Guid.NewGuid()) — no enruta a ninguna instancia de esta Saga.
        // CorrelateById (no CorrelateBy con predicado): CorrelateBy quedó descartado tras un test
        // de integración real contra MongoDbSagaRepository — el predicado nunca encontraba la
        // instancia (todo caía en OnMissingInstance aunque el GroupId sí coincidiera), a diferencia
        // del harness in-memory de los tests unitarios, donde sí funcionaba. CorrelateById consulta
        // directamente por _id (mismo camino, ya probado, que usa TicketPurchaseSaga para sus
        // propios eventos), sin depender de que el driver de Mongo sepa traducir un predicado
        // arbitrario. OnMissingInstance(Discard): sin esto, el comportamiento por defecto de
        // MassTransit ante un evento "During" sin instancia encontrada es Fault (no un simple "no
        // enruta") — habría reventado el flujo de compra suelta existente, que sigue publicando
        // estos 3 mensajes sin GroupId y por tanto sin ninguna PurchaseOrderSaga que los espere.
        Event(() => TicketReady, x =>
        {
            x.CorrelateById(context => context.Message.GroupId ?? Guid.Empty);
            x.OnMissingInstance(m => m.Discard());
        });
        Event(() => TicketConfirmed, x =>
        {
            x.CorrelateById(context => context.Message.GroupId ?? Guid.Empty);
            x.OnMissingInstance(m => m.Discard());
        });
        Event(() => TicketRejected, x =>
        {
            x.CorrelateById(context => context.Message.GroupId ?? Guid.Empty);
            x.OnMissingInstance(m => m.Discard());
        });

        Initially(
            When(OrderRequested)
                .Then(context =>
                {
                    context.Saga.EventId = context.Message.Id;
                    context.Saga.Tickets = context.Message.Tickets.ToList();
                    context.Saga.PendingTicketIds = context.Message.Tickets.Select(t => t.TicketId).ToList();
                    context.Saga.ReadyTicketIds = [];
                    context.Saga.ConfirmedTicketIds = [];
                    context.Saga.ExpectedCount = context.Message.Tickets.Count;
                })
                .TransitionTo(ReservingLines));

        During(ReservingLines,
            When(TicketReady)
                .Then(context =>
                {
                    context.Saga.PendingTicketIds.Remove(context.Message.CorrelationId);
                    context.Saga.ReadyTicketIds.Add(context.Message.CorrelationId);
                })
                .IfElse(context => context.Saga.PendingTicketIds.Count == 0,
                    allReserved => allReserved
                        .Publish(context => new PurchaseOrderAwaitingPayment(context.Saga.CorrelationId))
                        .TransitionTo(AwaitingPayment),
                    stillReserving => stillReserving),
            // "ZoneFull" (aforo lleno, ver ZoneService) es la única causa real de rechazo de
            // reserva en AccessControl hoy — se traduce a un motivo de pedido más claro para el
            // cliente; cualquier otro motivo (p. ej. "ReservationTimedOut", o "PaymentTimedOut" si
            // una línea hermana ya en AwaitingPayment agota su propio timeout de pago mientras
            // esta Saga sigue esperando a las demás) se deja igual, no hay nada más específico que
            // decir sobre él a nivel de pedido.
            When(TicketRejected)
                .Then(context =>
                {
                    context.Saga.RejectionReason = context.Message.Reason == "ZoneFull" ? "InsufficientAvailability" : context.Message.Reason;
                    // El ticket rechazado puede venir de Pending (seguía reservando) o de Ready (ya
                    // había reservado con éxito y ahora falla por su cuenta, p. ej. su propio
                    // PaymentTimedOut) — quitarlo solo de Pending dejaría un ticket ya finalizado
                    // colgado en Ready, y el bucle de abajo le publicaría un TicketPurchaseCancelled
                    // de más sobre una Saga de ticket que ya no existe (OnMissingInstance -> Fault).
                    context.Saga.PendingTicketIds.Remove(context.Message.CorrelationId);
                    context.Saga.ReadyTicketIds.Remove(context.Message.CorrelationId);
                })
                .ThenAsync(async context =>
                {
                    // Cancela TODAS las líneas hermanas sin desenlace conocido todavía, tanto las
                    // que seguían reservando (Pending) como las que ya habían reservado aforo con
                    // éxito (Ready) — ambas necesitan compensación, no solo las primeras.
                    foreach (var ticketId in context.Saga.PendingTicketIds.Concat(context.Saga.ReadyTicketIds))
                        await context.Publish(new TicketPurchaseCancelled(ticketId));

                    context.Saga.PendingTicketIds = context.Saga.PendingTicketIds.Concat(context.Saga.ReadyTicketIds).ToList();
                    context.Saga.ReadyTicketIds = [];
                })
                // Pedido de una sola línea (o última hermana ya resuelta): no queda nadie que
                // cancelar, así que ningún TicketRejected futuro va a volver a comprobar la
                // condición de cierre en CancellingLines — rechazar aquí mismo, no dejar la Saga
                // esperando un evento que nunca llegará.
                .IfElse(context => context.Saga.PendingTicketIds.Count == 0,
                    noSiblings => noSiblings
                        .Publish(context => new PurchaseOrderRejected(context.Saga.CorrelationId, context.Saga.RejectionReason!))
                        .TransitionTo(Rejected)
                        .Finalize(),
                    hasSiblings => hasSiblings.TransitionTo(CancellingLines)));

        During(CancellingLines,
            When(TicketRejected)
                .Then(context => context.Saga.PendingTicketIds.Remove(context.Message.CorrelationId))
                .IfElse(context => context.Saga.PendingTicketIds.Count == 0,
                    allCancelled => allCancelled
                        .Publish(context => new PurchaseOrderRejected(context.Saga.CorrelationId, context.Saga.RejectionReason!))
                        .TransitionTo(Rejected)
                        .Finalize(),
                    stillCancelling => stillCancelling));

        During(AwaitingPayment,
            When(OrderPaymentSubmitted)
                .Then(context => context.Saga.PendingTicketIds = context.Saga.Tickets.Select(t => t.TicketId).ToList())
                .ThenAsync(async context =>
                {
                    foreach (var ticketRef in context.Saga.Tickets)
                        await context.Publish(new TicketPaymentSubmitted(ticketRef.TicketId, context.Message.SimulateFailure));
                })
                .TransitionTo(ConfirmingLines),
            // Un TicketRejected puede llegar aquí por dos motivos distintos, sin que esta Saga haya
            // visto todavía OrderPaymentSubmitted:
            // (a) la línea agotó su propio timeout de pago (PaymentTimedOut) o el evento se canceló
            //     mientras esa línea seguía sin pagar (EventCancelled, ver
            //     TicketPurchaseSaga.During(AwaitingPayment)) — las hermanas siguen sin haber pagado
            //     de verdad, hay que cancelarlas (mismo patrón que ReservingLines).
            // (b) ventana de carrera equivalente a ADR 0027 pero a nivel de pedido: el pago YA se
            //     envió (la única vía por la que una TicketPurchaseSaga hermana rechaza con
            //     "PaymentFailed" es haber recibido TicketPaymentSubmitted, ver
            //     TicketPurchaseSaga.During(Confirming)), pero la transición de esta Saga a
            //     ConfirmingLines todavía no está persistida cuando ya llega el rechazo de la línea
            //     más rápida — hay que compensar a las hermanas ya confirmadas, no cancelarlas
            //     (mismo criterio que ConfirmingLines.When(TicketRejected)).
            // Sin esta rama en cualquiera de los dos casos: UnhandledEventException, mensaje a la
            // cola de error tras los reintentos, pedido colgado en AwaitingPayment para siempre — el
            // caso (b) se ha visto en real con una línea ya Confirmed y el pedido sin resolver.
            When(TicketRejected)
                .IfElse(context => context.Message.Reason is "PaymentTimedOut" or "EventCancelled",
                    notYetPaid => notYetPaid
                        .Then(context =>
                        {
                            context.Saga.RejectionReason = context.Message.Reason;
                            context.Saga.ReadyTicketIds.Remove(context.Message.CorrelationId);
                        })
                        .ThenAsync(async context =>
                        {
                            foreach (var ticketId in context.Saga.ReadyTicketIds)
                                await context.Publish(new TicketPurchaseCancelled(ticketId));

                            context.Saga.PendingTicketIds = context.Saga.ReadyTicketIds.ToList();
                            context.Saga.ReadyTicketIds = [];
                        })
                        // Pedido de una sola línea (o última hermana ya resuelta): no queda nadie
                        // que cancelar, así que no hay ningún TicketRejected futuro que vuelva a
                        // comprobar la condición de cierre en CancellingLines — rechazar aquí mismo,
                        // no dejar la Saga esperando un evento que nunca llegará.
                        .IfElse(context => context.Saga.PendingTicketIds.Count == 0,
                            noSiblings => noSiblings
                                .Publish(context => new PurchaseOrderRejected(context.Saga.CorrelationId, context.Saga.RejectionReason!))
                                .TransitionTo(Rejected)
                                .Finalize(),
                            hasSiblings => hasSiblings.TransitionTo(CancellingLines)),
                    alreadyPaid => alreadyPaid
                        .Then(context =>
                        {
                            context.Saga.RejectionReason ??= context.Message.Reason;
                            context.Saga.PendingTicketIds = context.Saga.Tickets
                                .Select(t => t.TicketId)
                                .Where(id => id != context.Message.CorrelationId)
                                .ToList();
                        })
                        .IfElse(context => context.Saga.PendingTicketIds.Count == 0,
                            allResolved => allResolved
                                .ThenAsync(context => CompensateConfirmedAsync(context.Saga, context))
                                .Publish(context => new PurchaseOrderRejected(context.Saga.CorrelationId, context.Saga.RejectionReason!))
                                .TransitionTo(Rejected)
                                .Finalize(),
                            stillConfirming => stillConfirming.TransitionTo(ConfirmingLines))),
            // Ventana de carrera equivalente a la de ADR 0027 pero a nivel de pedido: el pago ya se
            // envió (OrderPaymentSubmitted se procesó, es la única vía por la que una TicketPurchaseSaga
            // hermana recibe TicketPaymentSubmitted), pero la propia transición de esta Saga a
            // ConfirmingLines puede no estar aún persistida cuando ya llega la confirmación de una
            // línea que resolvió más rápido. Sin esta rama, el mismo UnhandledEventException deja el
            // pedido colgado en AwaitingPayment con una entrada ya Confirmed — visto en real. Se
            // resuelve igual que ConfirmingLines: registra la confirmación y, si no queda ninguna
            // pendiente, completa el pedido (no puede haber ninguna rechazada todavía, es la primera
            // resolución que llega).
            When(TicketConfirmed)
                .Then(context =>
                {
                    context.Saga.ConfirmedTicketIds.Add(context.Message.CorrelationId);
                    context.Saga.PendingTicketIds = context.Saga.Tickets
                        .Select(t => t.TicketId)
                        .Where(id => id != context.Message.CorrelationId)
                        .ToList();
                })
                .IfElse(context => context.Saga.PendingTicketIds.Count == 0,
                    allConfirmed => allConfirmed
                        .Publish(context => new PurchaseOrderCompleted(context.Saga.CorrelationId))
                        .TransitionTo(Completed)
                        .Finalize(),
                    stillConfirming => stillConfirming.TransitionTo(ConfirmingLines)));

        During(ConfirmingLines,
            When(TicketConfirmed)
                .Then(context =>
                {
                    context.Saga.ConfirmedTicketIds.Add(context.Message.CorrelationId);
                    context.Saga.PendingTicketIds.Remove(context.Message.CorrelationId);
                })
                .IfElse(context => context.Saga.PendingTicketIds.Count == 0,
                    allResolved => allResolved.IfElse(context => context.Saga.RejectionReason is null,
                        allConfirmed => allConfirmed
                            .Publish(context => new PurchaseOrderCompleted(context.Saga.CorrelationId))
                            .TransitionTo(Completed)
                            .Finalize(),
                        anyRejected => anyRejected
                            .ThenAsync(context => CompensateConfirmedAsync(context.Saga, context))
                            .Publish(context => new PurchaseOrderRejected(context.Saga.CorrelationId, context.Saga.RejectionReason!))
                            .TransitionTo(Rejected)
                            .Finalize()),
                    stillConfirming => stillConfirming),
            When(TicketRejected)
                .Then(context =>
                {
                    context.Saga.RejectionReason ??= context.Message.Reason;
                    context.Saga.PendingTicketIds.Remove(context.Message.CorrelationId);
                })
                .IfElse(context => context.Saga.PendingTicketIds.Count == 0,
                    allResolved => allResolved.IfElse(context => context.Saga.RejectionReason is null,
                        allConfirmed => allConfirmed
                            .Publish(context => new PurchaseOrderCompleted(context.Saga.CorrelationId))
                            .TransitionTo(Completed)
                            .Finalize(),
                        anyRejected => anyRejected
                            .ThenAsync(context => CompensateConfirmedAsync(context.Saga, context))
                            .Publish(context => new PurchaseOrderRejected(context.Saga.CorrelationId, context.Saga.RejectionReason!))
                            .TransitionTo(Rejected)
                            .Finalize()),
                    stillConfirming => stillConfirming));

        SetCompletedWhenFinalized();
    }

    /// <summary>Compensa las líneas hermanas YA confirmadas cuando otra línea falla al pagar —
    /// mismos dos mensajes que ya usa <c>EventCancelledConsumer</c> para reembolsar una entrada
    /// confirmada (ADR 0020/0021), sin mecanismo de compensación nuevo (ver ADR 0028).</summary>
    private static async Task CompensateConfirmedAsync(PurchaseOrderSagaState saga, IPublishEndpoint publishEndpoint)
    {
        var zoneByTicketId = saga.Tickets.ToDictionary(t => t.TicketId, t => t.ZoneId);

        foreach (var confirmedId in saga.ConfirmedTicketIds)
        {
            await publishEndpoint.Publish(new ReleaseConfirmedZoneSlot(confirmedId, zoneByTicketId[confirmedId]));
            await publishEndpoint.Publish(new TicketRefunded(confirmedId));
        }
    }
}
