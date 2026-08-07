namespace VenuePulse.AccessControl.Application;

public sealed class ZoneService(IZoneRepository zoneRepository, IZoneOccupancyNotifier notifier) : IZoneService
{
    public async Task<IReadOnlyCollection<ZoneOccupancyDto>> GetAllOccupancyAsync(CancellationToken cancellationToken)
    {
        var zones = await zoneRepository.GetAllAsync(cancellationToken);
        return zones.Select(ToDto).ToList();
    }

    public async Task<ZoneOccupancyDto?> GetOccupancyAsync(Guid zoneId, CancellationToken cancellationToken)
    {
        var zone = await zoneRepository.GetByIdAsync(zoneId, cancellationToken);
        return zone is null ? null : ToDto(zone);
    }

    public async Task<bool> TryReserveSlotAsync(Guid zoneId, CancellationToken cancellationToken)
    {
        var zone = await zoneRepository.GetByIdAsync(zoneId, cancellationToken)
            ?? throw new InvalidOperationException($"La zona {zoneId} no existe.");

        if (zone.AvailableToSell <= 0)
            return false;

        zone.ReserveSlot();
        await zoneRepository.SaveAsync(zone, cancellationToken);
        return true;
    }

    public async Task ConfirmSlotAsync(Guid zoneId, CancellationToken cancellationToken)
    {
        var zone = await zoneRepository.GetByIdAsync(zoneId, cancellationToken)
            ?? throw new InvalidOperationException($"La zona {zoneId} no existe.");

        zone.ConfirmSlot();
        await zoneRepository.SaveAsync(zone, cancellationToken);
    }

    public async Task ReleaseSlotAsync(Guid zoneId, CancellationToken cancellationToken)
    {
        var zone = await zoneRepository.GetByIdAsync(zoneId, cancellationToken)
            ?? throw new InvalidOperationException($"La zona {zoneId} no existe.");

        zone.ReleaseSlot();
        await zoneRepository.SaveAsync(zone, cancellationToken);
    }

    public async Task<int> RecordCheckInAsync(Guid zoneId, CancellationToken cancellationToken)
    {
        var zone = await zoneRepository.GetByIdAsync(zoneId, cancellationToken)
            ?? throw new InvalidOperationException($"La zona {zoneId} no existe.");

        zone.RecordCheckIn();
        await zoneRepository.SaveAsync(zone, cancellationToken);

        var dto = ToDto(zone);
        await notifier.NotifyOccupancyChangedAsync(dto, cancellationToken);

        return zone.CheckedIn;
    }

    private static ZoneOccupancyDto ToDto(Domain.Zone zone) =>
        new(zone.Id, zone.Name, zone.Capacity, zone.ConfirmedSold, zone.CheckedIn);
}
