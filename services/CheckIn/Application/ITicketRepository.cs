namespace VenuePulse.CheckIn.Application;

using VenuePulse.CheckIn.Domain;

/// <summary>
/// Deliberadamente estilo Unit-of-Work: Add/Get devuelven la entidad rastreada por el
/// DbContext y no guardan por sí solos — el handler decide cuándo llamar a
/// <see cref="SaveChangesAsync"/>, para poder publicar en el bus ANTES de guardar y que el
/// Outbox capture entrada + mensaje en la misma transacción (ver ADR 0002).
/// </summary>
public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Ticket>> GetByHolderAsync(string holder, CancellationToken cancellationToken);

    /// <summary>Usado por la cascada de cancelación (ADR 0021) y por el agregado de ingresos —
    /// ambos necesitan ver todos los estados de las entradas del evento, no solo una parte.</summary>
    Task<IReadOnlyList<Ticket>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken);

    Task AddAsync(Ticket ticket, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
