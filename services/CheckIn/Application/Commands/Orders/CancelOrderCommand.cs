namespace VenuePulse.CheckIn.Application.Commands;

using MediatR;
using MassTransit;
using VenuePulse.CheckIn.Domain;
using VenuePulse.Core.Domain;
using VenuePulse.Core.Messaging.Contracts;

public sealed record CancelOrderCommand(Guid OrderId) : IRequest<Result>;

public sealed class CancelOrderCommandHandler(
    IPurchaseOrderRepository orderRepository,
    ITicketRepository ticketRepository,
    IPublishEndpoint publishEndpoint) : IRequestHandler<CancelOrderCommand, Result>
{
    private const string CancelledByCustomerReason = "CancelledByCustomer";

    public async Task<Result> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result.Failure("El pedido no existe.");

        if (order.Status is not (PurchaseOrderStatus.Reserving or PurchaseOrderStatus.AwaitingPayment))
            return Result.Failure($"El pedido ya no se puede cancelar (estado actual: {order.Status}).");

        var tickets = await ticketRepository.GetByGroupIdAsync(order.Id, cancellationToken);

        foreach (var ticket in tickets)
        {
            if (ticket.Status is TicketStatus.Pending or TicketStatus.AwaitingPayment)
                await publishEndpoint.Publish(new TicketPurchaseCancelled(ticket.Id, CancelledByCustomerReason), cancellationToken);
        }

        await ticketRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
