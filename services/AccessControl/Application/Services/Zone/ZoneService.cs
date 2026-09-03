namespace VenuePulse.AccessControl.Application;

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Serilog;
using VenuePulse.Core.Domain;

public sealed class ZoneService(IZoneRepository zoneRepository, IOccupancySnapshotRepository snapshotRepository, IZoneOccupancyNotifier notifier) : IZoneService
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<ZoneService>();

    // AccessControl corre como una única instancia (sin réplicas, ver ADR 0029) — este lock en
    // memoria del proceso serializa por completo las mutaciones concurrentes sobre la MISMA zona,
    // cerrando de raíz la ventana de carrera que el reintento optimista (DbUpdateConcurrencyException)
    // no cubre: bajo alta contención real (varias líneas de un mismo pedido confirmando/liberando casi
    // a la vez), el guard de invariante del dominio (Reserved<=0) podía dispararse de forma transitoria
    // y no se reintentaba (ver ADR 0040).
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> ZoneLocks = new();

    private static readonly (string Name, int Capacity)[] MatchZones =
    [
        ("Grada VIP Norte", 8),
        ("Grada VIP Sur", 7),
        ("Grada Este", 40),
        ("Grada Oeste", 40),
    ];

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

        await snapshotRepository.ClearForEventAsync(eventId, cancellationToken);
    }

    public async Task ResetArrivalsSimulationAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var zones = await zoneRepository.GetAllAsync(eventId, cancellationToken);
        foreach (var zone in zones)
            await ExecuteWithRetryAsync(zone.Id, z => z.ResetCheckIns(), cancellationToken);

        await snapshotRepository.ClearForEventAsync(eventId, cancellationToken);
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        Guid zoneId, Func<Domain.Zone, (bool ShouldSave, T Result)> mutate, CancellationToken cancellationToken)
    {
        var zoneLock = ZoneLocks.GetOrAdd(zoneId, static _ => new SemaphoreSlim(1, 1));
        await zoneLock.WaitAsync(cancellationToken);
        try
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
        finally
        {
            zoneLock.Release();
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

        foreach (var zone in zones)
            await notifier.NotifyOccupancyChangedAsync(ToDto(zone), cancellationToken);
    }

    private static ZoneOccupancyDto ToDto(Domain.Zone zone) =>
        new(zone.Id, zone.EventId, zone.Name, zone.Capacity, zone.AvailableToSell, zone.ConfirmedSold, zone.CheckedIn, zone.Cancelled);
}
