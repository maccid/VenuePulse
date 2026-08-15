namespace VenuePulse.CheckIn.Application.Commands;

using MediatR;
using MassTransit;
using VenuePulse.Core.Domain;
using VenuePulse.Core.Messaging.Contracts;

public sealed record CheckInCommand(Guid TicketId) : IRequest<Result>;

public sealed class CheckInCommandHandler(ITicketRepository repository, IPublishEndpoint publishEndpoint)
    : IRequestHandler<CheckInCommand, Result>
{
    public async Task<Result> Handle(CheckInCommand request, CancellationToken cancellationToken)
    {
        var ticket = await repository.GetByIdAsync(request.TicketId, cancellationToken);
        if (ticket is null)
            return Result.Failure("La entrada no existe.");

        try
        {
            ticket.MarkCheckedIn();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }

        await publishEndpoint.Publish(
            new RecordZoneCheckIn(ticket.Id, ticket.ZoneId, ticket.Id),
            cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
