namespace VenuePulse.CheckIn.Application.Commands;

using MediatR;
using MassTransit;
using VenuePulse.CheckIn.Domain;
using VenuePulse.Core.Domain;
using VenuePulse.Core.Messaging.Contracts;

public sealed record SubmitPaymentCommand(Guid TicketId, bool SimulateFailure) : IRequest<Result>;

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
