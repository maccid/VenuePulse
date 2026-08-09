namespace VenuePulse.CheckIn.Application.Queries;

using MediatR;

public sealed record GetTicketStatusQuery(Guid TicketId) : IRequest<TicketStatusDto?>;

public sealed class GetTicketStatusQueryHandler(ITicketRepository repository)
    : IRequestHandler<GetTicketStatusQuery, TicketStatusDto?>
{
    public async Task<TicketStatusDto?> Handle(GetTicketStatusQuery request, CancellationToken cancellationToken)
    {
        var ticket = await repository.GetByIdAsync(request.TicketId, cancellationToken);
        return ticket is null
            ? null
            : new TicketStatusDto(ticket.Id, ticket.EventId, ticket.ZoneId, ticket.Status.ToString(), ticket.RejectionReason);
    }
}
