namespace VenuePulse.Ticketing.Application;

/// <summary>
/// Application Service simple, sin CQRS: el catálogo es mayormente de lectura y cambia poco,
/// separar comandos/consultas aquí sería ceremonia sin beneficio (ver ADR 0003).
/// </summary>
public interface ITicketingService
{
    Task<IReadOnlyCollection<EventSummaryDto>> GetEventsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TicketTypeDto>?> GetTicketTypesAsync(Guid eventId, CancellationToken cancellationToken);
}
