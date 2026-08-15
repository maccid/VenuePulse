namespace VenuePulse.CheckIn.Application.Queries;

using MediatR;

public sealed record GetOrderStatusQuery(Guid OrderId) : IRequest<OrderStatusDto?>;

public sealed class GetOrderStatusQueryHandler(
    IPurchaseOrderRepository orderRepository,
    ITicketRepository ticketRepository,
    IReservationTimeoutReader timeoutTracker) : IRequestHandler<GetOrderStatusQuery, OrderStatusDto?>
{
    public async Task<OrderStatusDto?> Handle(GetOrderStatusQuery request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null) return null;

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
