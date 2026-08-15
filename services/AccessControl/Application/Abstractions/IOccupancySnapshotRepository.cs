namespace VenuePulse.AccessControl.Application;

using VenuePulse.AccessControl.Domain;

public interface IOccupancySnapshotRepository
{
    Task AddRangeAsync(IReadOnlyCollection<ZoneOccupancySnapshot> snapshots, CancellationToken cancellationToken);

    Task ClearForEventAsync(Guid eventId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ZoneOccupancySnapshot>> GetForEventAsync(Guid eventId, CancellationToken cancellationToken);
}
