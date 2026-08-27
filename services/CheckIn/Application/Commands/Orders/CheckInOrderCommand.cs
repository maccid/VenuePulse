namespace VenuePulse.CheckIn.Application.Commands;

using MediatR;
using MassTransit;
using VenuePulse.CheckIn.Domain;
using VenuePulse.Core.Domain;
using VenuePulse.Core.Messaging.Contracts;

public sealed record CheckInOrderCommand(Guid GroupId) : IRequest<Result<int>>;

public sealed class CheckInOrderCommandHandler(ITicketRepository repository, IPublishEndpoint publishEndpoint)
    : IRequestHandler<CheckInOrderCommand, Result<int>>
{
    public async Task<Result<int>> Handle(CheckInOrderCommand request, CancellationToken cancellationToken)
    {
        var tickets = await repository.GetByGroupIdAsync(request.GroupId, cancellationToken);
        if (tickets.Count == 0)
            return Result.Failure<int>("El grupo no existe.");

        var checkedInCount = 0;
        foreach (var ticket in tickets)
        {
            if (ticket.Status != TicketStatus.Confirmed)
                continue;

            ticket.MarkCheckedIn();
            await publishEndpoint.Publish(new RecordZoneCheckIn(ticket.Id, ticket.ZoneId, ticket.Id), cancellationToken);
            checkedInCount++;
        }

        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success(checkedInCount);
    }
}
