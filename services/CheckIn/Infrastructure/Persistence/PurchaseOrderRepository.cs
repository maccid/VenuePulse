namespace VenuePulse.CheckIn.Infrastructure;

using Microsoft.EntityFrameworkCore;
using VenuePulse.CheckIn.Application;
using VenuePulse.CheckIn.Domain;

public sealed class PurchaseOrderRepository(CheckInDbContext dbContext) : IPurchaseOrderRepository
{
    public Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken) =>
        await dbContext.PurchaseOrders.AddAsync(order, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
