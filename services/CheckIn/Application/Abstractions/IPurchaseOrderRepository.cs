namespace VenuePulse.CheckIn.Application;

using VenuePulse.CheckIn.Domain;

public interface IPurchaseOrderRepository
{
    Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
