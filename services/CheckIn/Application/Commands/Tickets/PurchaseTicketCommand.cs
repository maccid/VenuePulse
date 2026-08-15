namespace VenuePulse.CheckIn.Application.Commands;

using MediatR;
using MassTransit;
using VenuePulse.CheckIn.Domain;
using VenuePulse.Core.Messaging.Contracts;

public sealed record PurchaseTicketCommand(
    Guid EventId,
    Guid TicketTypeId,
    Guid ZoneId,
    string Holder,
    decimal Price) : IRequest<Guid>;

public sealed class PurchaseTicketCommandHandler(ITicketRepository repository, IPublishEndpoint publishEndpoint)
    : IRequestHandler<PurchaseTicketCommand, Guid>
{
    public async Task<Guid> Handle(PurchaseTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = new Ticket(Guid.NewGuid(), request.EventId, request.TicketTypeId, request.ZoneId, request.Holder, request.Price);

        await repository.AddAsync(ticket, cancellationToken);

        await publishEndpoint.Publish(
            new TicketPurchaseRequested(ticket.Id, request.ZoneId),
            cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);

        return ticket.Id;
    }
}
