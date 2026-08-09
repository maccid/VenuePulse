namespace VenuePulse.Ticketing.Application;

using VenuePulse.Ticketing.Domain;

public sealed class TicketingService(IEventRepository eventRepository, IZoneDemandRepository zoneDemandRepository) : ITicketingService
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

        var occupancyPercentage = await zoneDemandRepository.GetOccupancyPercentageAsync(eventId, cancellationToken);

        return @event.TicketTypes
            .Select(t =>
            {
                var (effectivePrice, tier) = DynamicPricing.Apply(t.Price, occupancyPercentage);
                return new TicketTypeDto(t.Id, t.EventId, t.Name, t.Price, t.Description, effectivePrice, tier);
            })
            .ToList();
    }
}
