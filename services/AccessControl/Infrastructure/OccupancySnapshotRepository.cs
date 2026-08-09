namespace VenuePulse.AccessControl.Infrastructure;

using VenuePulse.AccessControl.Application;
using VenuePulse.AccessControl.Domain;

public sealed class OccupancySnapshotRepository(AccessControlDbContext dbContext) : IOccupancySnapshotRepository
{
    public async Task AddRangeAsync(IReadOnlyCollection<ZoneOccupancySnapshot> snapshots, CancellationToken cancellationToken)
    {
        if (snapshots.Count == 0)
            return;

        await dbContext.OccupancySnapshots.AddRangeAsync(snapshots, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearForEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var stale = dbContext.OccupancySnapshots.Where(s => s.EventId == eventId);
        dbContext.OccupancySnapshots.RemoveRange(stale);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
