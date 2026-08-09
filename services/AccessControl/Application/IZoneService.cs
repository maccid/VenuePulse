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

    /// <summary>Venta simulada (motor de demo, Bloque 8): confirma entradas sin pasar por la
    /// Saga, respetando el aforo vendible.</summary>
    Task<ZoneOccupancyDto> ApplySimulatedSalesAsync(Guid zoneId, int quantity, CancellationToken cancellationToken);

    /// <summary>Check-in simulado (motor de demo, Bloque 8): hace pasar por el torniquete sin
    /// entrada real, nunca por encima de lo confirmado vendido.</summary>
    Task<ZoneOccupancyDto> ApplySimulatedCheckInsAsync(Guid zoneId, int quantity, CancellationToken cancellationToken);

    /// <summary>Pone a cero los contadores de todas las zonas del evento (reiniciar el demo).</summary>
    Task ResetSimulationAsync(Guid eventId, CancellationToken cancellationToken);
}
