namespace VenuePulse.CheckIn.Application.Commands;

using MediatR;
using MassTransit;
using VenuePulse.CheckIn.Domain;
using VenuePulse.Core.Domain;
using VenuePulse.Core.Messaging.Contracts;

public sealed record PlaceOrderLineRequest(Guid TicketTypeId, Guid ZoneId, int Quantity, decimal UnitPrice);

public sealed record PlaceOrderCommand(Guid EventId, string Holder, IReadOnlyList<PlaceOrderLineRequest> Lines) : IRequest<Result<Guid>>;

/// <summary>
/// Pedido multi-entrada atómico (ADR 0028): crea UN <see cref="PurchaseOrder"/> y N
/// <see cref="Ticket"/> hermanos (uno por unidad de cada línea), todos vinculados por
/// <see cref="Ticket.GroupId"/> = <see cref="PurchaseOrder.Id"/>, y dispara tanto sus N
/// <c>TicketPurchaseSaga</c> individuales (<see cref="TicketPurchaseRequested"/> por entrada) como
/// la <c>PurchaseOrderSaga</c> orquestadora (<see cref="PurchaseOrderRequested"/>) — todo en la
/// MISMA transacción vía el Outbox (Add -> Publish -> SaveChanges, un único
/// <see cref="ITicketRepository.SaveChangesAsync"/> al final, ver <see cref="ITicketRepository"/>).
/// La validación de límite/precio ocurre ANTES de tocar ningún repositorio o publicar nada — un
/// pedido rechazado en esta fase no deja ningún rastro (ni Ticket ni PurchaseOrder).
/// </summary>
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
            // Precio desactualizado en CUALQUIER línea rechaza el pedido completo (ADR 0028) — sin
            // tocar ningún repositorio ni publicar nada, ni siquiera para las líneas que sí cuadran.
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

                await publishEndpoint.Publish(
                    new TicketPurchaseRequested(ticket.Id, line.ZoneId, order.Id),
                    cancellationToken);

                ticketRefs.Add(new PurchaseOrderTicketRef(ticket.Id, line.ZoneId));
            }
        }

        await publishEndpoint.Publish(
            new PurchaseOrderRequested(order.Id, request.EventId, ticketRefs),
            cancellationToken);

        await ticketRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(order.Id);
    }
}
