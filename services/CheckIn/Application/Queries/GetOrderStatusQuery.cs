namespace VenuePulse.CheckIn.Application.Queries;

using MediatR;

public sealed record GetOrderStatusQuery(Guid OrderId) : IRequest<OrderStatusDto?>;

/// <summary>
/// Compone el estado agregado de un pedido (ADR 0028) leyendo su cabecera
/// (<see cref="IPurchaseOrderRepository"/>) y derivando sus líneas agrupando las entradas
/// hermanas por <see cref="Domain.Ticket.GroupId"/> (<see cref="ITicketRepository.GetByGroupIdAsync"/>) —
/// el pedido no guarda líneas propias, ver ADR 0028 "Alternativas descartadas". Mismo patrón de
/// <see cref="IReservationTimeoutReader"/> que <see cref="GetTicketStatusQueryHandler"/> para el
/// contador de reserva de cada línea.
/// </summary>
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
