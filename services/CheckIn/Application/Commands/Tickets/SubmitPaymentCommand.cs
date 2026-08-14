namespace VenuePulse.CheckIn.Application.Commands;

using MediatR;
using MassTransit;
using VenuePulse.CheckIn.Domain;
using VenuePulse.Core.Domain;
using VenuePulse.Core.Messaging.Contracts;

public sealed record SubmitPaymentCommand(Guid TicketId, bool SimulateFailure) : IRequest<Result>;

/// <summary>
/// El cliente elige método de pago (Tarjeta/Bizum, decidido en la Api) para una entrada que ya
/// reservó su aforo y está esperando en el estado "AwaitingPayment" de la Saga (ver
/// TicketPurchaseSaga). No hay cambio de agregado que persistir aquí (el Ticket lo mutan más
/// tarde los consumers de confirmación/rechazo), pero el Outbox transaccional (ADR 0002) solo
/// vacía a la Saga lo que `IPublishEndpoint.Publish` encola en el MISMO DbContext cuando se llama
/// a SaveChangesAsync — sin esa llamada el mensaje se queda solo en el change tracker y nunca sale
/// de verdad, aunque la Api responda 202 igualmente.
/// </summary>
public sealed class SubmitPaymentCommandHandler(ITicketRepository repository, IPublishEndpoint publishEndpoint)
    : IRequestHandler<SubmitPaymentCommand, Result>
{
    public async Task<Result> Handle(SubmitPaymentCommand request, CancellationToken cancellationToken)
    {
        var ticket = await repository.GetByIdAsync(request.TicketId, cancellationToken);
        if (ticket is null)
            return Result.Failure("La entrada no existe.");

        if (ticket.Status != TicketStatus.AwaitingPayment)
            return Result.Failure($"La entrada no está esperando pago todavía (estado actual: {ticket.Status}).");

        await publishEndpoint.Publish(new TicketPaymentSubmitted(ticket.Id, request.SimulateFailure), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
