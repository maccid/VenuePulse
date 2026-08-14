namespace VenuePulse.CheckIn.Infrastructure.Consumers;

using MassTransit;
using Microsoft.Extensions.Logging;
using VenuePulse.CheckIn.Application;
using VenuePulse.CheckIn.Infrastructure.Sagas.Timeout;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>
/// Compone el <see cref="OrderStatusDto"/> completo a partir de la cabecera del pedido y de sus
/// líneas (entradas agrupadas por <c>GroupId</c>, ver ADR 0028) — mismo patrón que
/// <c>GetOrderStatusQueryHandler</c>, reutilizado por los 3 consumers de este fichero para no
/// repetir la composición.
/// </summary>
internal static class OrderStatusComposer
{
    public static async Task<OrderStatusDto> ComposeAsync(
        VenuePulse.CheckIn.Domain.PurchaseOrder order,
        ITicketRepository ticketRepository,
        ReservationTimeoutTracker timeoutTracker,
        CancellationToken cancellationToken)
    {
        var tickets = await ticketRepository.GetByGroupIdAsync(order.Id, cancellationToken);

        var ticketDtos = tickets
            .Select(t =>
            {
                var expiresAtUtc = timeoutTracker.TryGetDeadline(t.Id, out var deadline) ? deadline : (DateTimeOffset?)null;
                return new OrderTicketStatusDto(t.Id, t.ZoneId, t.TicketTypeId, t.Price, t.Status.ToString(), t.RejectionReason, expiresAtUtc);
            })
            .ToList();

        return new OrderStatusDto(order.Id, order.EventId, order.Status.ToString(), order.RejectionReason, ticketDtos);
    }
}

/// <summary>
/// La <c>PurchaseOrderSaga</c> ya transicionó a "AwaitingPayment" (todas sus líneas reservaron
/// aforo) — refleja ese hecho en el <see cref="VenuePulse.CheckIn.Domain.PurchaseOrder"/>, mismo
/// motivo que <c>TicketReadyForPaymentConsumer</c> (ADR 0018/0028): escucha el evento publicado
/// por la Saga tras persistir su propia transición, no un evento en paralelo con ella.
/// </summary>
public sealed class PurchaseOrderReadyForPaymentConsumer(
    IPurchaseOrderRepository orderRepository,
    ITicketRepository ticketRepository,
    IOrderStatusNotifier notifier,
    ReservationTimeoutTracker timeoutTracker,
    ILogger<PurchaseOrderReadyForPaymentConsumer> logger) : IConsumer<PurchaseOrderAwaitingPayment>
{
    public async Task Consume(ConsumeContext<PurchaseOrderAwaitingPayment> context)
    {
        var order = await orderRepository.GetByIdAsync(context.Message.CorrelationId, context.CancellationToken);
        if (order is null)
        {
            logger.LogWarning("PurchaseOrderAwaitingPayment para un pedido inexistente {OrderId}", context.Message.CorrelationId);
            return;
        }

        order.MarkAwaitingPayment();
        await orderRepository.SaveChangesAsync(context.CancellationToken);

        var status = await OrderStatusComposer.ComposeAsync(order, ticketRepository, timeoutTracker, context.CancellationToken);
        await notifier.NotifyStatusChangedAsync(status, context.CancellationToken);
    }
}

public sealed class PurchaseOrderCompletedConsumer(
    IPurchaseOrderRepository orderRepository,
    ITicketRepository ticketRepository,
    IOrderStatusNotifier notifier,
    ReservationTimeoutTracker timeoutTracker,
    ILogger<PurchaseOrderCompletedConsumer> logger) : IConsumer<PurchaseOrderCompleted>
{
    public async Task Consume(ConsumeContext<PurchaseOrderCompleted> context)
    {
        var order = await orderRepository.GetByIdAsync(context.Message.CorrelationId, context.CancellationToken);
        if (order is null)
        {
            logger.LogWarning("PurchaseOrderCompleted para un pedido inexistente {OrderId}", context.Message.CorrelationId);
            return;
        }

        order.Complete();
        await orderRepository.SaveChangesAsync(context.CancellationToken);

        var status = await OrderStatusComposer.ComposeAsync(order, ticketRepository, timeoutTracker, context.CancellationToken);
        await notifier.NotifyStatusChangedAsync(status, context.CancellationToken);
    }
}

public sealed class PurchaseOrderRejectedConsumer(
    IPurchaseOrderRepository orderRepository,
    ITicketRepository ticketRepository,
    IOrderStatusNotifier notifier,
    ReservationTimeoutTracker timeoutTracker,
    ILogger<PurchaseOrderRejectedConsumer> logger) : IConsumer<PurchaseOrderRejected>
{
    public async Task Consume(ConsumeContext<PurchaseOrderRejected> context)
    {
        var order = await orderRepository.GetByIdAsync(context.Message.CorrelationId, context.CancellationToken);
        if (order is null)
        {
            logger.LogWarning("PurchaseOrderRejected para un pedido inexistente {OrderId}", context.Message.CorrelationId);
            return;
        }

        order.Reject(context.Message.Reason);
        await orderRepository.SaveChangesAsync(context.CancellationToken);

        var status = await OrderStatusComposer.ComposeAsync(order, ticketRepository, timeoutTracker, context.CancellationToken);
        await notifier.NotifyStatusChangedAsync(status, context.CancellationToken);
    }
}
