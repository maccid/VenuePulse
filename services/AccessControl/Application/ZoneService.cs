namespace VenuePulse.AccessControl.Application;

using VenuePulse.Core.Domain;

public sealed class ZoneService(IZoneRepository zoneRepository, IOccupancySnapshotRepository snapshotRepository, IZoneOccupancyNotifier notifier) : IZoneService
{
    // Set de zonas fijo por tipo de evento — igual para cualquier partido/concierto (sin
    // geometría real, ver ADR sobre StadiumMap). Los nombres y aforos quedan fijados en el plan de
    // la ronda (sección "zonas y precios por tipo de evento"): 4 zonas en partido (2 puertas
    // generales + 2 gradas VIP simétricas) y 5 en concierto (pista VIP + pista + frontal +
    // laterales). El emparejamiento zona↔tipo de entrada lo resuelve el frontend por nombre
    // (coincidencia exacta → prefijo, ver ADR 0016/0018), sin relación real en el backend.
    private static readonly (string Name, int Capacity)[] MatchZones =
    [
        ("Puerta Norte", 45),
        ("Puerta Sur", 45),
        ("Grada VIP Este", 20),
        ("Grada VIP Oeste", 20),
    ];

    private static readonly (string Name, int Capacity)[] ConcertZones =
    [
        ("Pista VIP", 15),
        ("Pista", 55),
        ("Grada Frontal", 15),
        ("Grada Lateral Este", 25),
        ("Grada Lateral Oeste", 25),
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

        // Zona cancelada = evento cancelado (ADR 0020): la reserva se rechaza igual que si estuviera
        // llena. El motivo "ZoneFull" en el fallo es un pelo impreciso, pero la Saga solo necesita
        // saber "no se pudo reservar" para rechazar la compra — no merece un tipo de fallo nuevo.
        if (zone.Cancelled || zone.AvailableToSell <= 0)
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

    public async Task CancelEventZonesAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var zones = await zoneRepository.GetAllAsync(eventId, cancellationToken);
        foreach (var zone in zones)
        {
            zone.Cancel();
            await zoneRepository.SaveAsync(zone, cancellationToken);
            await notifier.NotifyOccupancyChangedAsync(ToDto(zone), cancellationToken);
        }
    }

    public async Task ReleaseConfirmedSlotAsync(Guid zoneId, CancellationToken cancellationToken)
    {
        var zone = await zoneRepository.GetByIdAsync(zoneId, cancellationToken)
            ?? throw new InvalidOperationException($"La zona {zoneId} no existe.");

        zone.ReleaseConfirmedSlot();
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
        new(zone.Id, zone.EventId, zone.Name, zone.Capacity, zone.AvailableToSell, zone.ConfirmedSold, zone.CheckedIn, zone.Cancelled);
}
