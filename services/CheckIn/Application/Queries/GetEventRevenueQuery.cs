namespace VenuePulse.CheckIn.Application.Queries;

using MediatR;
using VenuePulse.CheckIn.Domain;

public sealed record GetEventRevenueQuery(Guid EventId) : IRequest<EventRevenueDto>;

public sealed class GetEventRevenueQueryHandler(ITicketRepository repository)
    : IRequestHandler<GetEventRevenueQuery, EventRevenueDto>
{
    public async Task<EventRevenueDto> Handle(GetEventRevenueQuery request, CancellationToken cancellationToken)
    {
        var tickets = await repository.GetByEventIdAsync(request.EventId, cancellationToken);

        var confirmed = tickets.Where(t => t.Status is TicketStatus.Confirmed or TicketStatus.CheckedIn).ToList();
        var pending = tickets.Where(t => t.Status is TicketStatus.AwaitingPayment or TicketStatus.ProcessingPayment).ToList();

        return new EventRevenueDto(
            request.EventId,
            confirmed.Sum(t => t.Price),
            confirmed.Count,
            pending.Sum(t => t.Price),
            pending.Count);
    }
}
