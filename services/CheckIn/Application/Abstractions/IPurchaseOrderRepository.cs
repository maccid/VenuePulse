namespace VenuePulse.CheckIn.Application;

using VenuePulse.CheckIn.Domain;

/// <summary>
/// Mismo patrón Unit-of-Work que <see cref="ITicketRepository"/> (ver ADR 0002/0028): Add/Get
/// devuelven la entidad rastreada por el DbContext, el handler decide cuándo llamar a
/// <see cref="SaveChangesAsync"/> para poder publicar en el bus ANTES de guardar (Outbox).
/// </summary>
public interface IPurchaseOrderRepository
{
    Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
