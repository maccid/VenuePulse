namespace VenuePulse.AccessControl.Application;

using Microsoft.EntityFrameworkCore;
using Serilog;
using VenuePulse.Core.Domain;

public sealed class ZoneService(IZoneRepository zoneRepository, IOccupancySnapshotRepository snapshotRepository, IZoneOccupancyNotifier notifier) : IZoneService
{
    // Logger estático vía Serilog (no ILogger<T> por constructor): ZoneService se instancia a mano
    // en 12 tests de AccessControl.Tests — inyectar ILogger<> los rompería todos sin aportar nada.
    private static readonly Serilog.ILogger Logger = Log.ForContext<ZoneService>();

    // Set de zonas fijo por tipo de evento — igual para cualquier partido/concierto (sin
    // geometría real, ver ADR 0017). Los nombres de zona "especiales" coinciden a propósito con el
    // TicketType correspondiente en Ticketing, para que el frontend pueda fusionar zona+tipo de
    // entrada por nombre sin inventar una relación cruzada. Partido tiene 4 zonas (las 2 gradas de
    // fondo, Norte/Sur, son las VIP caras y con menos aforo; las 2 laterales, Este/Oeste, son las
    // normales) — ver ADR 0025, que invierte qué par de gradas es VIP respecto a ADR 0024.
    private static readonly (string Name, int Capacity)[] MatchZones =
    [
        ("Grada VIP Norte", 8),
        ("Grada VIP Sur", 7),
        ("Grada Este", 40),
        ("Grada Oeste", 40),
    ];

    // Concierto tiene 6 zonas (ver ADR 0026): 3 gradas al mismo precio alrededor del recinto,
    // Pista de pie a precio medio, y 2 VIP junto al escenario al precio más alto de las tres —
    // aforo total 100, igual que el set anterior de 3 zonas, por continuidad.
    private static readonly (string Name, int Capacity)[] ConcertZones =
    [
        ("Grada 1", 15),
        ("Grada 2", 15),
        ("Grada 3", 15),
        ("Pista", 35),
        ("VIP 1", 10),
        ("VIP 2", 10),
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

    public Task<bool> TryReserveSlotAsync(Guid zoneId, CancellationToken cancellationToken) =>
        ExecuteWithRetryAsync(zoneId, zone =>
        {
            // Zona cancelada = evento cancelado (ADR 0020): la reserva se rechaza igual que si
            // estuviera llena. El motivo "ZoneFull" en el fallo es un pelo impreciso, pero la Saga
            // solo necesita saber "no se pudo reservar" para rechazar la compra — no merece un tipo
            // de fallo nuevo. Se re-evalúa en cada intento sobre datos frescos: si la zona se llenó
            // mientras reintentábamos por un conflicto de concurrencia, el resultado debe reflejarlo.
            if (zone.Cancelled || zone.AvailableToSell <= 0)
                return (ShouldSave: false, Result: false);

            zone.ReserveSlot();
            return (ShouldSave: true, Result: true);
        }, cancellationToken);

    public Task ConfirmSlotAsync(Guid zoneId, CancellationToken cancellationToken) =>
        ExecuteWithRetryAsync(zoneId, zone => zone.ConfirmSlot(), cancellationToken);

    public Task ReleaseSlotAsync(Guid zoneId, CancellationToken cancellationToken) =>
        ExecuteWithRetryAsync(zoneId, zone => zone.ReleaseSlot(), cancellationToken);

    public async Task CancelEventZonesAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var zones = await zoneRepository.GetAllAsync(eventId, cancellationToken);
        foreach (var zone in zones)
            await ExecuteWithRetryAsync(zone.Id, z => z.Cancel(), cancellationToken);
    }

    public Task ReleaseConfirmedSlotAsync(Guid zoneId, CancellationToken cancellationToken) =>
        ExecuteWithRetryAsync(zoneId, zone => zone.ReleaseConfirmedSlot(), cancellationToken);

    public Task<int> RecordCheckInAsync(Guid zoneId, CancellationToken cancellationToken) =>
        ExecuteWithRetryAsync(zoneId, zone =>
        {
            zone.RecordCheckIn();
            return (ShouldSave: true, Result: zone.CheckedIn);
        }, cancellationToken);

    public Task<ZoneOccupancyDto> ApplySimulatedSalesAsync(Guid zoneId, int quantity, CancellationToken cancellationToken) =>
        ExecuteWithRetryAsync(zoneId, zone =>
        {
            zone.RecordSimulatedSales(quantity);
            return (ShouldSave: true, Result: ToDto(zone));
        }, cancellationToken);

    public Task<ZoneOccupancyDto> ApplySimulatedCheckInsAsync(Guid zoneId, int quantity, CancellationToken cancellationToken) =>
        ExecuteWithRetryAsync(zoneId, zone =>
        {
            zone.RecordSimulatedCheckIns(quantity);
            return (ShouldSave: true, Result: ToDto(zone));
        }, cancellationToken);

    public async Task ResetSimulationAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var zones = await zoneRepository.GetAllAsync(eventId, cancellationToken);
        foreach (var zone in zones)
            await ExecuteWithRetryAsync(zone.Id, z => z.ResetCounters(), cancellationToken);

        // Sin esto, la predicción (Bloque 11) acumula snapshots de corridas anteriores del mismo
        // evento y se envenena: puntos fuera de la ventana quedan pegados a progress 0/1 y el R²
        // se degrada. El reset debe dejar la serie temporal limpia para empezar de cero.
        await snapshotRepository.ClearForEventAsync(eventId, cancellationToken);
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        Guid zoneId, Func<Domain.Zone, (bool ShouldSave, T Result)> mutate, CancellationToken cancellationToken)
    {
        const int maxAttempts = 15;
        for (var attempt = 1; ; attempt++)
        {
            var zone = await zoneRepository.GetByIdAsync(zoneId, cancellationToken)
                ?? throw new InvalidOperationException($"La zona {zoneId} no existe.");

            var (shouldSave, result) = mutate(zone);
            if (!shouldSave)
                return result;

            try
            {
                await zoneRepository.SaveAsync(zone, cancellationToken);
                await notifier.NotifyOccupancyChangedAsync(ToDto(zone), cancellationToken);
                return result;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxAttempts)
            {
                Logger.Warning("Conflicto de concurrencia en la zona {ZoneId} (intento {Attempt}/{MaxAttempts}) — reintentando", zoneId, attempt, maxAttempts);
            }
        }
    }

    private Task ExecuteWithRetryAsync(Guid zoneId, Action<Domain.Zone> mutate, CancellationToken cancellationToken) =>
        ExecuteWithRetryAsync<bool>(zoneId, zone =>
        {
            mutate(zone);
            return (ShouldSave: true, Result: true);
        }, cancellationToken);

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
