namespace VenuePulse.AccessControl.Infrastructure;

using Microsoft.EntityFrameworkCore;
using VenuePulse.AccessControl.Application;
using VenuePulse.AccessControl.Domain;

public sealed class ZoneRepository(AccessControlDbContext dbContext) : IZoneRepository
{
    public async Task<IReadOnlyList<Zone>> GetAllAsync(Guid? eventId, CancellationToken cancellationToken)
    {
        var query = dbContext.Zones.AsNoTracking();
        if (eventId is not null)
            query = query.Where(z => z.EventId == eventId);

        return await query.OrderBy(z => z.Name).ToListAsync(cancellationToken);
    }

    public Task<Zone?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Zones.FirstOrDefaultAsync(z => z.Id == id, cancellationToken);

    public Task<bool> ExistsForEventAsync(Guid eventId, CancellationToken cancellationToken) =>
        dbContext.Zones.AnyAsync(z => z.EventId == eventId, cancellationToken);

    public async Task AddRangeAsync(IReadOnlyCollection<Zone> zones, CancellationToken cancellationToken)
    {
        await dbContext.Zones.AddRangeAsync(zones, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAsync(Zone zone, CancellationToken cancellationToken)
    {
        if (dbContext.Entry(zone).State == EntityState.Detached)
        {
            dbContext.Zones.Attach(zone);
            dbContext.Entry(zone).State = EntityState.Modified;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
