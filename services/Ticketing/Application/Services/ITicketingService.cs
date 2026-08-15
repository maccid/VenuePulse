namespace VenuePulse.Ticketing.Application;

public enum EventLifecycleResult
{
    Success,
    NotFound,
    InvalidState,
}

public interface ITicketingService
{
    Task<IReadOnlyCollection<EventSummaryDto>> GetEventsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TicketTypeDto>?> GetTicketTypesAsync(Guid eventId, CancellationToken cancellationToken);

    Task<EventSummaryDto> CreateEventAsync(CreateEventRequest request, CancellationToken cancellationToken);

    Task<EventLifecycleResult> CloseSalesAsync(Guid eventId, CancellationToken cancellationToken);

    Task<EventLifecycleResult> CancelEventAsync(Guid eventId, CancellationToken cancellationToken);
}
