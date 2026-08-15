namespace VenuePulse.Ticketing.Application;

using MassTransit;
using VenuePulse.Core.Messaging.Contracts;
using VenuePulse.Ticketing.Domain;
using Event = VenuePulse.Ticketing.Domain.Event;

public sealed class TicketingService(
    IEventRepository eventRepository,
    IZoneDemandRepository zoneDemandRepository,
    IPublishEndpoint publishEndpoint) : ITicketingService
{
    private const string Venue = "Estadio Central";

    public async Task<IReadOnlyCollection<EventSummaryDto>> GetEventsAsync(CancellationToken cancellationToken)
    {
        var events = await eventRepository.GetAllAsync(cancellationToken);

        return events
            .Select(ToSummary)
            .ToList();
    }

    public async Task<IReadOnlyCollection<TicketTypeDto>?> GetTicketTypesAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var @event = await eventRepository.GetByIdAsync(eventId, cancellationToken);
        if (@event is null)
            return null;

        var occupancyPercentage = await zoneDemandRepository.GetOccupancyPercentageAsync(eventId, cancellationToken);
        var relevanceMultiplier = @event.Relevance.Multiplier();

        return @event.TicketTypes
            .Select(t =>
            {
                var adjustedBasePrice = t.Price * relevanceMultiplier;
                var (effectivePrice, tier) = DynamicPricing.Apply(adjustedBasePrice, occupancyPercentage);
                return new TicketTypeDto(t.Id, t.EventId, t.Name, t.Price, adjustedBasePrice, t.Description, effectivePrice, tier);
            })
            .ToList();
    }

    public async Task<EventSummaryDto> CreateEventAsync(CreateEventRequest request, CancellationToken cancellationToken)
    {
        var @event = new Event(Guid.NewGuid(), request.Name, Venue, request.StartsAtUtc, request.Type, request.Relevance);

        foreach (var (name, price, description) in DefaultTicketTypeCatalog.For(request.Type))
            @event.AddTicketType(Guid.NewGuid(), name, price, description);

        await eventRepository.AddAsync(@event, cancellationToken);

        await publishEndpoint.Publish(
            new EventCreated(@event.Id, @event.Name, @event.VenueName, @event.StartsAtUtc, @event.Type),
            cancellationToken);

        await eventRepository.SaveChangesAsync(cancellationToken);

        return ToSummary(@event);
    }

    public async Task<EventLifecycleResult> CloseSalesAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var @event = await eventRepository.GetByIdAsync(eventId, cancellationToken);
        if (@event is null)
            return EventLifecycleResult.NotFound;

        if (@event.Status != EventStatus.OnSale)
            return EventLifecycleResult.InvalidState;

        @event.CloseSales();
        await eventRepository.SaveChangesAsync(cancellationToken);

        return EventLifecycleResult.Success;
    }

    public async Task<EventLifecycleResult> CancelEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var @event = await eventRepository.GetByIdAsync(eventId, cancellationToken);
        if (@event is null)
            return EventLifecycleResult.NotFound;

        if (@event.Status != EventStatus.OnSale)
            return EventLifecycleResult.InvalidState;

        @event.Cancel();

        await publishEndpoint.Publish(new EventCancelled(@event.Id), cancellationToken);

        await eventRepository.SaveChangesAsync(cancellationToken);

        return EventLifecycleResult.Success;
    }

    private static EventSummaryDto ToSummary(Event e) =>
        new(e.Id, e.Name, e.VenueName, e.StartsAtUtc, e.Type, e.Status, e.Relevance);
}
