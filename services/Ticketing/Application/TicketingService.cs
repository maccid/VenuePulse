namespace VenuePulse.Ticketing.Application;

public sealed class TicketingService(IEventRepository eventRepository) : ITicketingService
{
    public async Task<IReadOnlyCollection<EventSummaryDto>> GetEventsAsync(CancellationToken cancellationToken)
    {
        var events = await eventRepository.GetAllAsync(cancellationToken);

        return events
            .Select(e => new EventSummaryDto(e.Id, e.Name, e.VenueName, e.StartsAtUtc))
            .ToList();
    }

    public async Task<IReadOnlyCollection<TicketTypeDto>?> GetTicketTypesAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var @event = await eventRepository.GetByIdAsync(eventId, cancellationToken);
        if (@event is null)
            return null;

        return @event.TicketTypes
            .Select(t => new TicketTypeDto(t.Id, t.EventId, t.Name, t.Price, t.Description))
            .ToList();
    }
}
