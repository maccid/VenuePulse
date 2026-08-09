namespace VenuePulse.CheckIn.Application.Commands;

using MediatR;
using MassTransit;
using VenuePulse.CheckIn.Domain;
using VenuePulse.Core.Messaging.Contracts;

public sealed record PurchaseTicketCommand(
    Guid EventId,
    Guid TicketTypeId,
    Guid ZoneId,
    string Holder) : IRequest<Guid>;

/// <summary>
/// Escritura compleja: crea la entrada (Pending) y dispara la Saga publicando el evento
/// iniciador dentro de la MISMA transacción vía el Outbox (Add -> Publish -> SaveChanges, en
/// ese orden — ver <see cref="ITicketRepository"/>). Reserva el aforo y deja la entrada
/// esperando a que el cliente elija método de pago (ver <see cref="SubmitPaymentCommand"/>).
/// </summary>
public sealed class PurchaseTicketCommandHandler(ITicketRepository repository, IPublishEndpoint publishEndpoint)
    : IRequestHandler<PurchaseTicketCommand, Guid>
{
    public async Task<Guid> Handle(PurchaseTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = new Ticket(Guid.NewGuid(), request.EventId, request.TicketTypeId, request.ZoneId, request.Holder);

        await repository.AddAsync(ticket, cancellationToken);

        await publishEndpoint.Publish(
            new TicketPurchaseRequested(ticket.Id, request.ZoneId),
            cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);

        return ticket.Id;
    }
}
