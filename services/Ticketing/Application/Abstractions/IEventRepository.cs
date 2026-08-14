namespace VenuePulse.Ticketing.Application;

using VenuePulse.Ticketing.Domain;

public interface IEventRepository
{
    Task<IReadOnlyList<Event>> GetAllAsync(CancellationToken cancellationToken);

    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Solo trackea el alta — no guarda. Permite publicar el evento de integración
    /// entre Add y SaveChanges (Outbox transaccional), mismo patrón que ITicketRepository en
    /// CheckIn (ver ADR 0019).</summary>
    Task AddAsync(Event @event, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
