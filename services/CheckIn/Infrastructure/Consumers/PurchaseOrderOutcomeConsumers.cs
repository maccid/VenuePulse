namespace VenuePulse.CheckIn.Infrastructure.Consumers;

using MassTransit;
using Microsoft.Extensions.Logging;
using VenuePulse.CheckIn.Application;
using VenuePulse.CheckIn.Infrastructure.Sagas.Timeout;
using VenuePulse.Core.Messaging.Contracts;

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

/// <summary>Consume PurchaseOrderAwaitingPayment: marca el pedido como esperando pago y notifica el nuevo estado.</summary>
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

/// <summary>Consume PurchaseOrderPaymentSubmitted: marca el pedido como confirmando líneas y notifica el nuevo estado.</summary>
public sealed class PurchaseOrderConfirmingConsumer(
    IPurchaseOrderRepository orderRepository,
    ITicketRepository ticketRepository,
    IOrderStatusNotifier notifier,
    ReservationTimeoutTracker timeoutTracker,
    ILogger<PurchaseOrderConfirmingConsumer> logger) : IConsumer<PurchaseOrderPaymentSubmitted>
{
    public async Task Consume(ConsumeContext<PurchaseOrderPaymentSubmitted> context)
    {
        var order = await orderRepository.GetByIdAsync(context.Message.CorrelationId, context.CancellationToken);
        if (order is null)
        {
            logger.LogWarning("PurchaseOrderPaymentSubmitted para un pedido inexistente {OrderId}", context.Message.CorrelationId);
            return;
        }

        order.MarkConfirming();
        await orderRepository.SaveChangesAsync(context.CancellationToken);

        var status = await OrderStatusComposer.ComposeAsync(order, ticketRepository, timeoutTracker, context.CancellationToken);
        await notifier.NotifyStatusChangedAsync(status, context.CancellationToken);
    }
}

/// <summary>Consume PurchaseOrderCompleted: marca el pedido como completado y notifica el nuevo estado.</summary>
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

/// <summary>Consume PurchaseOrderRejected: marca el pedido como rechazado y notifica el nuevo estado.</summary>
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
