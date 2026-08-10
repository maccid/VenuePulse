namespace VenuePulse.Ticketing.Application;

/// <summary>Resultado de una transición de ciclo de vida (cerrar venta/cancelar) — evita mezclar
/// "no existe" (404) con "estado no válido" (409) en un único Result de texto libre, sin llegar
/// a montar una taxonomía de errores completa para un caso tan acotado (ver ADR 0019).</summary>
public enum EventLifecycleResult
{
    Success,
    NotFound,
    InvalidState,
}

/// <summary>
/// Application Service simple, sin CQRS: el catálogo es mayormente de lectura y cambia poco,
/// separar comandos/consultas aquí sería ceremonia sin beneficio (ver ADR 0003).
/// </summary>
public interface ITicketingService
{
    Task<IReadOnlyCollection<EventSummaryDto>> GetEventsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TicketTypeDto>?> GetTicketTypesAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>Alta de evento desde Operaciones — mismo patrón Add→Publish→SaveChanges
    /// (Outbox) que ya usa el seed data para EventCreated (ver ADR 0019).</summary>
    Task<EventSummaryDto> CreateEventAsync(CreateEventRequest request, CancellationToken cancellationToken);

    Task<EventLifecycleResult> CloseSalesAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>Publica EventCancelled (Outbox) si el evento estaba OnSale — AccessControl y
    /// CheckIn lo consumen para cerrar zonas y reembolsar entradas (ver ADR 0019/0020/0021).
    /// </summary>
    Task<EventLifecycleResult> CancelEventAsync(Guid eventId, CancellationToken cancellationToken);
}
