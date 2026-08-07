namespace VenuePulse.CheckIn.Application.Queries;

using MediatR;

public sealed record GetMyTicketsQuery(string Holder) : IRequest<IReadOnlyCollection<TicketSummaryDto>>;

public sealed class GetMyTicketsQueryHandler(ITicketRepository repository)
    : IRequestHandler<GetMyTicketsQuery, IReadOnlyCollection<TicketSummaryDto>>
{
    public async Task<IReadOnlyCollection<TicketSummaryDto>> Handle(GetMyTicketsQuery request, CancellationToken cancellationToken)
    {
        var tickets = await repository.GetByHolderAsync(request.Holder, cancellationToken);

        return tickets
            .Select(t => new TicketSummaryDto(t.Id, t.EventId, t.ZoneId, t.Holder, t.Status.ToString(), t.CreatedAtUtc))
            .ToList();
    }
}
