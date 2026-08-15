namespace VenuePulse.CheckIn.Infrastructure.Sagas;

using MassTransit;
using Serilog;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>Orquesta un pedido multi-entrada por encima de N TicketPurchaseSaga aisladas: todo-o-nada al reservar, compensa al pagar si alguna línea falla.</summary>
public sealed class PurchaseOrderSaga : MassTransitStateMachine<PurchaseOrderSagaState>
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<PurchaseOrderSaga>();

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
                    context.Saga.ExpectedCount = context.Message.Tickets.Count;
                    Logger.Information("Pedido {OrderId} solicitado con {Count} entradas", context.Saga.CorrelationId, context.Saga.ExpectedCount);
                })
                .TransitionTo(ReservingLines));

        During(ReservingLines,
            When(TicketReady)
                .Then(context =>
                {
                    context.Saga.PendingTicketIds.Remove(context.Message.CorrelationId);
                    context.Saga.ReadyTicketIds.Add(context.Message.CorrelationId);
                    Logger.Information("Línea {TicketId} lista en el pedido {OrderId} ({Pending} pendientes)", context.Message.CorrelationId, context.Saga.CorrelationId, context.Saga.PendingTicketIds.Count);
                })
                .IfElse(context => context.Saga.PendingTicketIds.Count == 0,
                    allReserved => allReserved
                        .Publish(context => new PurchaseOrderAwaitingPayment(context.Saga.CorrelationId))
                        .TransitionTo(AwaitingPayment),
                    stillReserving => stillReserving),
            When(TicketRejected)
                .Then(context =>
                {
                    context.Saga.RejectionReason = context.Message.Reason == "ZoneFull" ? "InsufficientAvailability" : context.Message.Reason;
                    Logger.Warning("Línea {TicketId} rechazada al reservar en el pedido {OrderId}: {Reason}", context.Message.CorrelationId, context.Saga.CorrelationId, context.Message.Reason);
                    context.Saga.PendingTicketIds.Remove(context.Message.CorrelationId);
                    context.Saga.ReadyTicketIds.Remove(context.Message.CorrelationId);
                })
                .ThenAsync(async context =>
                {
                    foreach (var ticketId in context.Saga.PendingTicketIds.Concat(context.Saga.ReadyTicketIds))
                        await context.Publish(new TicketPurchaseCancelled(ticketId));

                    context.Saga.PendingTicketIds = context.Saga.PendingTicketIds.Concat(context.Saga.ReadyTicketIds).ToList();
                    context.Saga.ReadyTicketIds = [];
                })
                .IfElse(context => context.Saga.PendingTicketIds.Count == 0,
                    noSiblings => noSiblings
                        .Then(context => Logger.Warning("Pedido {OrderId} rechazado sin líneas hermanas que cancelar: {Reason}", context.Saga.CorrelationId, context.Saga.RejectionReason))
                        .Publish(context => new PurchaseOrderRejected(context.Saga.CorrelationId, context.Saga.RejectionReason!))
                        .TransitionTo(Rejected)
                        .Finalize(),
                    hasSiblings => hasSiblings
                        .Then(context => Logger.Warning("Pedido {OrderId} falló — cancelando {Count} líneas hermanas", context.Saga.CorrelationId, context.Saga.PendingTicketIds.Count))
                        .TransitionTo(CancellingLines)));

        During(CancellingLines,
            When(TicketRejected)
                .Then(context =>
                {
                    context.Saga.PendingTicketIds.Remove(context.Message.CorrelationId);
                    Logger.Information("Línea {TicketId} cancelada en el pedido {OrderId}", context.Message.CorrelationId, context.Saga.CorrelationId);
                })
                .IfElse(context => context.Saga.PendingTicketIds.Count == 0,
                    allCancelled => allCancelled
                        .Then(context => Logger.Warning("Pedido {OrderId} rechazado tras cancelar todas las líneas: {Reason}", context.Saga.CorrelationId, context.Saga.RejectionReason))
                        .Publish(context => new PurchaseOrderRejected(context.Saga.CorrelationId, context.Saga.RejectionReason!))
                        .TransitionTo(Rejected)
                        .Finalize(),
                    stillCancelling => stillCancelling));

        During(AwaitingPayment,
            When(OrderPaymentSubmitted)
                .Then(context =>
                {
                    context.Saga.PendingTicketIds = context.Saga.Tickets.Select(t => t.TicketId).ToList();
                    Logger.Information("Pago del pedido {OrderId} recibido", context.Saga.CorrelationId);
                })
                .ThenAsync(async context =>
                {
                    foreach (var ticketRef in context.Saga.Tickets)
                        await context.Publish(new TicketPaymentSubmitted(ticketRef.TicketId, context.Message.SimulateFailure));
                })
                .TransitionTo(ConfirmingLines),
            When(TicketRejected)
                .IfElse(context => context.Message.Reason is "PaymentTimedOut" or "EventCancelled",
                    notYetPaid => notYetPaid
                        .Then(context =>
                        {
                            context.Saga.RejectionReason = context.Message.Reason;
                            context.Saga.ReadyTicketIds.Remove(context.Message.CorrelationId);
                            Logger.Warning("Línea {TicketId} rechazada sin pagar en el pedido {OrderId}: {Reason}", context.Message.CorrelationId, context.Saga.CorrelationId, context.Message.Reason);
                        })
                        .ThenAsync(async context =>
                        {
                            foreach (var ticketId in context.Saga.ReadyTicketIds)
                                await context.Publish(new TicketPurchaseCancelled(ticketId));

                            context.Saga.PendingTicketIds = context.Saga.ReadyTicketIds.ToList();
                            context.Saga.ReadyTicketIds = [];
                        })
                        .IfElse(context => context.Saga.PendingTicketIds.Count == 0,
                            noSiblings => noSiblings
                                .Then(context => Logger.Warning("Pedido {OrderId} rechazado sin pagar: {Reason}", context.Saga.CorrelationId, context.Saga.RejectionReason))
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
                            Logger.Warning("Línea {TicketId} rechazada tras el pago en el pedido {OrderId}: {Reason}", context.Message.CorrelationId, context.Saga.CorrelationId, context.Message.Reason);
                        })
                        .IfElse(context => context.Saga.PendingTicketIds.Count == 0,
                            allResolved => allResolved
                                .ThenAsync(context => CompensateConfirmedAsync(context.Saga, context))
                                .Then(context => Logger.Warning("Pedido {OrderId} rechazado compensando líneas confirmadas: {Reason}", context.Saga.CorrelationId, context.Saga.RejectionReason))
                                .Publish(context => new PurchaseOrderRejected(context.Saga.CorrelationId, context.Saga.RejectionReason!))
                                .TransitionTo(Rejected)
                                .Finalize(),
                            stillConfirming => stillConfirming.TransitionTo(ConfirmingLines))),
            When(TicketConfirmed)
                .Then(context =>
                {
                    context.Saga.ConfirmedTicketIds.Add(context.Message.CorrelationId);
                    context.Saga.PendingTicketIds = context.Saga.Tickets
                        .Select(t => t.TicketId)
                        .Where(id => id != context.Message.CorrelationId)
                        .ToList();
                    Logger.Information("Línea {TicketId} confirmada antes de la transición en el pedido {OrderId}", context.Message.CorrelationId, context.Saga.CorrelationId);
                })
                .IfElse(context => context.Saga.PendingTicketIds.Count == 0,
                    allConfirmed => allConfirmed
                        .Then(context => Logger.Information("Pedido {OrderId} completado", context.Saga.CorrelationId))
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
                    Logger.Information("Línea {TicketId} confirmada en el pedido {OrderId} ({Pending} pendientes)", context.Message.CorrelationId, context.Saga.CorrelationId, context.Saga.PendingTicketIds.Count);
                })
                .IfElse(context => context.Saga.PendingTicketIds.Count == 0,
                    allResolved => allResolved.IfElse(context => context.Saga.RejectionReason is null,
                        allConfirmed => allConfirmed
                            .Then(context => Logger.Information("Pedido {OrderId} completado", context.Saga.CorrelationId))
                            .Publish(context => new PurchaseOrderCompleted(context.Saga.CorrelationId))
                            .TransitionTo(Completed)
                            .Finalize(),
                        anyRejected => anyRejected
                            .ThenAsync(context => CompensateConfirmedAsync(context.Saga, context))
                            .Then(context => Logger.Warning("Pedido {OrderId} rechazado compensando líneas confirmadas: {Reason}", context.Saga.CorrelationId, context.Saga.RejectionReason))
                            .Publish(context => new PurchaseOrderRejected(context.Saga.CorrelationId, context.Saga.RejectionReason!))
                            .TransitionTo(Rejected)
                            .Finalize()),
                    stillConfirming => stillConfirming),
            When(TicketRejected)
                .Then(context =>
                {
                    context.Saga.RejectionReason ??= context.Message.Reason;
                    context.Saga.PendingTicketIds.Remove(context.Message.CorrelationId);
                    Logger.Warning("Línea {TicketId} rechazada en confirmación del pedido {OrderId}: {Reason}", context.Message.CorrelationId, context.Saga.CorrelationId, context.Message.Reason);
                })
                .IfElse(context => context.Saga.PendingTicketIds.Count == 0,
                    allResolved => allResolved.IfElse(context => context.Saga.RejectionReason is null,
                        allConfirmed => allConfirmed
                            .Then(context => Logger.Information("Pedido {OrderId} completado", context.Saga.CorrelationId))
                            .Publish(context => new PurchaseOrderCompleted(context.Saga.CorrelationId))
                            .TransitionTo(Completed)
                            .Finalize(),
                        anyRejected => anyRejected
                            .ThenAsync(context => CompensateConfirmedAsync(context.Saga, context))
                            .Then(context => Logger.Warning("Pedido {OrderId} rechazado compensando líneas confirmadas: {Reason}", context.Saga.CorrelationId, context.Saga.RejectionReason))
                            .Publish(context => new PurchaseOrderRejected(context.Saga.CorrelationId, context.Saga.RejectionReason!))
                            .TransitionTo(Rejected)
                            .Finalize()),
                    stillConfirming => stillConfirming));

        SetCompletedWhenFinalized();
    }

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
