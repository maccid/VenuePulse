namespace VenuePulse.AccessControl.Infrastructure;

using Microsoft.EntityFrameworkCore;
using VenuePulse.AccessControl.Application;
using VenuePulse.AccessControl.Domain;

public sealed class ZoneRepository(AccessControlDbContext dbContext) : IZoneRepository
{
    public async Task<IReadOnlyList<Zone>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Zones.AsNoTracking().OrderBy(z => z.Name).ToListAsync(cancellationToken);

    public Task<Zone?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Zones.FirstOrDefaultAsync(z => z.Id == id, cancellationToken);

    public async Task SaveAsync(Zone zone, CancellationToken cancellationToken)
    {
        if (dbContext.Entry(zone).State == EntityState.Detached)
            dbContext.Zones.Attach(zone);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
