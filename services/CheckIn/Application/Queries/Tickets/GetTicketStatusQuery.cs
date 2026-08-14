namespace VenuePulse.CheckIn.Application.Queries;

using MediatR;

public sealed record GetTicketStatusQuery(Guid TicketId) : IRequest<TicketStatusDto?>;

public sealed class GetTicketStatusQueryHandler(ITicketRepository repository, IReservationTimeoutReader timeoutTracker)
    : IRequestHandler<GetTicketStatusQuery, TicketStatusDto?>
{
    public async Task<TicketStatusDto?> Handle(GetTicketStatusQuery request, CancellationToken cancellationToken)
    {
        var ticket = await repository.GetByIdAsync(request.TicketId, cancellationToken);
        if (ticket is null) return null;

        var expiresAtUtc = timeoutTracker.TryGetDeadline(ticket.Id, out var deadline) ? deadline : (DateTimeOffset?)null;
        return new TicketStatusDto(ticket.Id, ticket.EventId, ticket.ZoneId, ticket.Status.ToString(), ticket.RejectionReason, expiresAtUtc);
    }
}
