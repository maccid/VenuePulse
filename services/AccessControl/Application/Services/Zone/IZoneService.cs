namespace VenuePulse.AccessControl.Application;

using VenuePulse.Core.Domain;

public interface IZoneService
{
    Task<IReadOnlyCollection<ZoneOccupancyDto>> GetAllOccupancyAsync(Guid? eventId, CancellationToken cancellationToken);

    Task<ZoneOccupancyDto?> GetOccupancyAsync(Guid zoneId, CancellationToken cancellationToken);

    Task<bool> TryReserveSlotAsync(Guid zoneId, CancellationToken cancellationToken);

    Task ConfirmSlotAsync(Guid zoneId, CancellationToken cancellationToken);

    Task ReleaseSlotAsync(Guid zoneId, CancellationToken cancellationToken);

    Task<int> RecordCheckInAsync(Guid zoneId, CancellationToken cancellationToken);

    Task CancelEventZonesAsync(Guid eventId, CancellationToken cancellationToken);

    Task ReleaseConfirmedSlotAsync(Guid zoneId, CancellationToken cancellationToken);

    Task ProvisionZonesForEventAsync(Guid eventId, EventType eventType, CancellationToken cancellationToken);

    Task<ZoneOccupancyDto> ApplySimulatedSalesAsync(Guid zoneId, int quantity, CancellationToken cancellationToken);

    Task<ZoneOccupancyDto> ApplySimulatedCheckInsAsync(Guid zoneId, int quantity, CancellationToken cancellationToken);

    Task ResetSimulationAsync(Guid eventId, CancellationToken cancellationToken);

    Task ResetArrivalsSimulationAsync(Guid eventId, CancellationToken cancellationToken);
}
