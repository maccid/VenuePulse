namespace VenuePulse.AccessControl.Application;

using VenuePulse.AccessControl.Domain;

/// <summary>
/// Persistencia de la serie temporal de ocupación generada por el motor de simulación
/// (Bloque 8) — materia prima del Bloque 11.
/// </summary>
public interface IOccupancySnapshotRepository
{
    Task AddRangeAsync(IReadOnlyCollection<ZoneOccupancySnapshot> snapshots, CancellationToken cancellationToken);

    Task ClearForEventAsync(Guid eventId, CancellationToken cancellationToken);
}
