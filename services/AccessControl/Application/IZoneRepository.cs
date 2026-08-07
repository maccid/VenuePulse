namespace VenuePulse.AccessControl.Application;

using VenuePulse.AccessControl.Domain;

public interface IZoneRepository
{
    Task<IReadOnlyList<Zone>> GetAllAsync(CancellationToken cancellationToken);

    Task<Zone?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task SaveAsync(Zone zone, CancellationToken cancellationToken);
}
