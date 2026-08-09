namespace VenuePulse.AccessControl.Application;

using VenuePulse.Core.Domain;

public sealed class ZoneService(IZoneRepository zoneRepository, IOccupancySnapshotRepository snapshotRepository, IZoneOccupancyNotifier notifier) : IZoneService
{
    // Set de zonas fijo por tipo de evento — igual para cualquier partido/concierto (sin
    // geometría real, ver ADR sobre StadiumMap): "Grada VIP"/"Pista" son las zonas "especiales"
    // cuyo nombre coincide a propósito con el TicketType correspondiente en Ticketing, para que el
    // frontend pueda fusionar zona+tipo de entrada por nombre sin inventar una relación cruzada.
    private static readonly (string Name, int Capacity)[] MatchZones =
    [
        ("Puerta Norte", 40),
        ("Puerta Sur", 40),
        ("Grada VIP", 15),
    ];

    private static readonly (string Name, int Capacity)[] ConcertZones =
    [
        ("Pista", 60),
        ("Grada Norte", 20),
        ("Grada Sur", 20),
    ];

    private static (string Name, int Capacity)[] ZonesFor(EventType eventType) => eventType switch
    {
        EventType.Concert => ConcertZones,
        _ => MatchZones,
    };

    public async Task<IReadOnlyCollection<ZoneOccupancyDto>> GetAllOccupancyAsync(Guid? eventId, CancellationToken cancellationToken)
    {
        var zones = await zoneRepository.GetAllAsync(eventId, cancellationToken);
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
        await notifier.NotifyOccupancyChangedAsync(ToDto(zone), cancellationToken);
        return true;
    }

    public async Task ConfirmSlotAsync(Guid zoneId, CancellationToken cancellationToken)
    {
        var zone = await zoneRepository.GetByIdAsync(zoneId, cancellationToken)
            ?? throw new InvalidOperationException($"La zona {zoneId} no existe.");

        zone.ConfirmSlot();
        await zoneRepository.SaveAsync(zone, cancellationToken);
        await notifier.NotifyOccupancyChangedAsync(ToDto(zone), cancellationToken);
    }

    public async Task ReleaseSlotAsync(Guid zoneId, CancellationToken cancellationToken)
    {
        var zone = await zoneRepository.GetByIdAsync(zoneId, cancellationToken)
            ?? throw new InvalidOperationException($"La zona {zoneId} no existe.");

        zone.ReleaseSlot();
        await zoneRepository.SaveAsync(zone, cancellationToken);
        await notifier.NotifyOccupancyChangedAsync(ToDto(zone), cancellationToken);
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

    public async Task<ZoneOccupancyDto> ApplySimulatedSalesAsync(Guid zoneId, int quantity, CancellationToken cancellationToken)
    {
        var zone = await zoneRepository.GetByIdAsync(zoneId, cancellationToken)
            ?? throw new InvalidOperationException($"La zona {zoneId} no existe.");

        zone.RecordSimulatedSales(quantity);
        await zoneRepository.SaveAsync(zone, cancellationToken);

        var dto = ToDto(zone);
        await notifier.NotifyOccupancyChangedAsync(dto, cancellationToken);

        return dto;
    }

    public async Task<ZoneOccupancyDto> ApplySimulatedCheckInsAsync(Guid zoneId, int quantity, CancellationToken cancellationToken)
    {
        var zone = await zoneRepository.GetByIdAsync(zoneId, cancellationToken)
            ?? throw new InvalidOperationException($"La zona {zoneId} no existe.");

        zone.RecordSimulatedCheckIns(quantity);
        await zoneRepository.SaveAsync(zone, cancellationToken);

        var dto = ToDto(zone);
        await notifier.NotifyOccupancyChangedAsync(dto, cancellationToken);

        return dto;
    }

    public async Task ResetSimulationAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var zones = await zoneRepository.GetAllAsync(eventId, cancellationToken);
        foreach (var zone in zones)
        {
            zone.ResetCounters();
            await zoneRepository.SaveAsync(zone, cancellationToken);
            await notifier.NotifyOccupancyChangedAsync(ToDto(zone), cancellationToken);
        }

        // Sin esto, la predicción (Bloque 11) acumula snapshots de corridas anteriores del mismo
        // evento y se envenena: puntos fuera de la ventana quedan pegados a progress 0/1 y el R²
        // se degrada. El reset debe dejar la serie temporal limpia para empezar de cero.
        await snapshotRepository.ClearForEventAsync(eventId, cancellationToken);
    }

    public async Task ProvisionZonesForEventAsync(Guid eventId, EventType eventType, CancellationToken cancellationToken)
    {
        if (await zoneRepository.ExistsForEventAsync(eventId, cancellationToken))
            return;

        var zones = ZonesFor(eventType)
            .Select(z => new Domain.Zone(Guid.NewGuid(), eventId, z.Name, z.Capacity))
            .ToList();

        await zoneRepository.AddRangeAsync(zones, cancellationToken);

        // Notifica el estado inicial de cada zona: sin esto, Ticketing (Bloque 9) solo se entera
        // de una zona la primera vez que cambia, y una zona virgen (100% libre) queda fuera de su
        // agregado de ocupación por evento — infla el % de demanda calculado en vez de diluirlo.
        foreach (var zone in zones)
            await notifier.NotifyOccupancyChangedAsync(ToDto(zone), cancellationToken);
    }

    private static ZoneOccupancyDto ToDto(Domain.Zone zone) =>
        new(zone.Id, zone.EventId, zone.Name, zone.Capacity, zone.AvailableToSell, zone.ConfirmedSold, zone.CheckedIn);
}
