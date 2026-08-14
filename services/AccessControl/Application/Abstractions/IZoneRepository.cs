namespace VenuePulse.AccessControl.Application;

using VenuePulse.AccessControl.Domain;

public interface IZoneRepository
{
    Task<IReadOnlyList<Zone>> GetAllAsync(Guid? eventId, CancellationToken cancellationToken);

    Task<Zone?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsForEventAsync(Guid eventId, CancellationToken cancellationToken);

    Task AddRangeAsync(IReadOnlyCollection<Zone> zones, CancellationToken cancellationToken);

    Task SaveAsync(Zone zone, CancellationToken cancellationToken);
}
