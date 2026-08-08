namespace VenuePulse.AccessControl.Application;

/// <summary>
/// Application Service simple, sin CQRS (ver ADR 0003): reservar/confirmar/liberar/registrar
/// check-in es CRUD con un par de invariantes, separar comandos y consultas aquí sería
/// ceremonia sin beneficio real.
/// </summary>
public interface IZoneService
{
    Task<IReadOnlyCollection<ZoneOccupancyDto>> GetAllOccupancyAsync(Guid? eventId, CancellationToken cancellationToken);

    Task<ZoneOccupancyDto?> GetOccupancyAsync(Guid zoneId, CancellationToken cancellationToken);

    Task<bool> TryReserveSlotAsync(Guid zoneId, CancellationToken cancellationToken);

    Task ConfirmSlotAsync(Guid zoneId, CancellationToken cancellationToken);

    Task ReleaseSlotAsync(Guid zoneId, CancellationToken cancellationToken);

    Task<int> RecordCheckInAsync(Guid zoneId, CancellationToken cancellationToken);

    /// <summary>Aprovisiona las 3 zonas fijas del recinto para un evento nuevo. Idempotente:
    /// si el evento ya tiene zonas (redelivery de EventCreated) no hace nada.</summary>
    Task ProvisionZonesForEventAsync(Guid eventId, CancellationToken cancellationToken);
}
