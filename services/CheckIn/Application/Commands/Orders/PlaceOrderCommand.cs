namespace VenuePulse.CheckIn.Application.Commands;

using MediatR;
using MassTransit;
using VenuePulse.CheckIn.Domain;
using VenuePulse.Core.Domain;
using VenuePulse.Core.Messaging.Contracts;

public sealed record PlaceOrderLineRequest(Guid TicketTypeId, Guid ZoneId, int Quantity, decimal UnitPrice);

public sealed record PlaceOrderCommand(Guid EventId, string Holder, IReadOnlyList<PlaceOrderLineRequest> Lines) : IRequest<Result<Guid>>;

public sealed class PlaceOrderCommandHandler(
    ITicketRepository ticketRepository,
    IPurchaseOrderRepository orderRepository,
    ITicketingClient ticketingClient,
    IPublishEndpoint publishEndpoint) : IRequestHandler<PlaceOrderCommand, Result<Guid>>
{
    public const int MaxTicketsPerOrder = 10;

    public async Task<Result<Guid>> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var totalQuantity = request.Lines.Sum(l => l.Quantity);
        if (totalQuantity > MaxTicketsPerOrder)
            return Result.Failure<Guid>("TooManyTickets");

        var ticketTypesResult = await ticketingClient.GetTicketTypesAsync(request.EventId, cancellationToken);
        if (ticketTypesResult.IsFailure)
            return Result.Failure<Guid>("PricingUnavailable");

        var ticketTypesById = ticketTypesResult.Value.ToDictionary(t => t.Id);

        foreach (var line in request.Lines)
        {
            if (!ticketTypesById.TryGetValue(line.TicketTypeId, out var ticketType) || ticketType.EffectivePrice != line.UnitPrice)
                return Result.Failure<Guid>("PriceChanged");
        }

        var order = new PurchaseOrder(Guid.NewGuid(), request.EventId, request.Holder);
        await orderRepository.AddAsync(order, cancellationToken);

        var ticketRefs = new List<PurchaseOrderTicketRef>();

        foreach (var line in request.Lines)
        {
            for (var i = 0; i < line.Quantity; i++)
            {
                var ticket = new Ticket(Guid.NewGuid(), request.EventId, line.TicketTypeId, line.ZoneId, request.Holder, line.UnitPrice);
                ticket.AssignToGroup(order.Id);

                await ticketRepository.AddAsync(ticket, cancellationToken);

                ticketRefs.Add(new PurchaseOrderTicketRef(ticket.Id, line.ZoneId));
            }
        }

        await publishEndpoint.Publish(
            new PurchaseOrderRequested(order.Id, request.EventId, ticketRefs),
            cancellationToken);

        foreach (var ticketRef in ticketRefs)
        {
            await publishEndpoint.Publish(
                new TicketPurchaseRequested(ticketRef.TicketId, ticketRef.ZoneId, order.Id),
                cancellationToken);
        }

        await ticketRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(order.Id);
    }
}
