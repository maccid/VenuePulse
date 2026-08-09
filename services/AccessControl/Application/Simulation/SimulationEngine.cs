namespace VenuePulse.AccessControl.Application.Simulation;

using System.Collections.Concurrent;
using VenuePulse.AccessControl.Application;

/// <summary>
/// Motor de simulación de demo (Bloque 8). Singleton con estado en memoria: un run por evento,
/// sin tocar Ticketing. Cada tick del worker avanza el reloj virtual y aplica las curvas a las
/// zonas vía <see cref="IZoneService"/>, emitiendo <see cref="Domain.ZoneOccupancySnapshot"/>
/// para el Bloque 11. Como es un singleton, los servicios scoped (zonas, snapshots) se pasan
/// como parámetro desde el worker — ver Infrastructure.SimulationTickWorker.
/// </summary>
public interface ISimulationEngine
{
    /// <summary>Registra un run para el evento. Devuelve false si ya hay uno en curso.</summary>
    Task<bool> TryStartAsync(Guid eventId, SimulationStartRequest request, CancellationToken cancellationToken);

    bool Stop(Guid eventId);

    SimulationRunStatusDto? GetStatus(Guid eventId);

    /// <summary>Avanza todos los runs activos un tick. Lo invoca el worker desde un scope.</summary>
    Task AdvanceAsync(
        DateTimeOffset nowUtc,
        IZoneService zoneService,
        IOccupancySnapshotRepository snapshotRepository,
        CancellationToken cancellationToken);
}

public sealed class SimulationEngine : ISimulationEngine
{
    /// <summary>Ventana de venta: los 7 días previos al evento.</summary>
    private static readonly TimeSpan SalesWindow = TimeSpan.FromDays(7);

    /// <summary>Ventana de llegada: las 3 horas previas al evento.</summary>
    private static readonly TimeSpan ArrivalsWindow = TimeSpan.FromHours(3);

    private readonly ConcurrentDictionary<Guid, SimulationRun> _runs = new();

    public Task<bool> TryStartAsync(Guid eventId, SimulationStartRequest request, CancellationToken cancellationToken)
    {
        if (_runs.ContainsKey(eventId))
            return Task.FromResult(false);

        var window = request.Mode switch
        {
            SimulationMode.Sales => SalesWindow,
            SimulationMode.Arrivals => ArrivalsWindow,
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Mode, null),
        };

        var run = new SimulationRun(
            eventId,
            request.Mode,
            request.EventStartsAtUtc,
            window,
            request.DurationSeconds,
            request.TargetSoldPct,
            request.NoShowPct);

        _runs[eventId] = run;
        return Task.FromResult(true);
    }

    public bool Stop(Guid eventId) => _runs.TryRemove(eventId, out _);

    public SimulationRunStatusDto? GetStatus(Guid eventId)
    {
        if (!_runs.TryGetValue(eventId, out var run))
            return null;

        var progress = run.Progress(DateTimeOffset.UtcNow);
        return new SimulationRunStatusDto(eventId, run.Mode, true, progress, run.VirtualNowUtc(DateTimeOffset.UtcNow));
    }

    public async Task AdvanceAsync(
        DateTimeOffset nowUtc,
        IZoneService zoneService,
        IOccupancySnapshotRepository snapshotRepository,
        CancellationToken cancellationToken)
    {
        foreach (var run in _runs.Values.ToArray())
        {
            var progress = run.Progress(nowUtc);
            var virtualNowUtc = run.VirtualNowUtc(nowUtc);
            var completed = progress >= 1;

            var zones = await zoneService.GetAllOccupancyAsync(run.EventId, cancellationToken);

            foreach (var zone in zones)
            {
                if (run.Mode == SimulationMode.Sales)
                {
                    var targetSold = (int)Math.Round(zone.Capacity * run.TargetSoldPct * SimulationCurves.Sales(progress));
                    var delta = Math.Min(targetSold - zone.ConfirmedSold, zone.AvailableToSell);
                    if (delta > 0)
                        await zoneService.ApplySimulatedSalesAsync(zone.ZoneId, delta, cancellationToken);
                }
                else
                {
                    var targetCheckedIn = (int)Math.Round(zone.ConfirmedSold * (1 - run.NoShowPct) * SimulationCurves.Arrivals(progress));
                    var delta = targetCheckedIn - zone.CheckedIn;
                    if (delta > 0)
                        await zoneService.ApplySimulatedCheckInsAsync(zone.ZoneId, delta, cancellationToken);
                }
            }

            // El tick que completa el run aplica los últimos incrementos pero NO escribe snapshot:
            // su reloj virtual sobrepasaría el evento (VirtualNowUtc no se clampa) y ese punto
            // "post-evento" corrompe la predicción (Bloque 11) al quedar como el más reciente.
            if (!completed)
            {
                var snapshots = new List<Domain.ZoneOccupancySnapshot>(zones.Count);
                foreach (var zone in zones)
                {
                    var updated = await zoneService.GetOccupancyAsync(zone.ZoneId, cancellationToken);
                    if (updated is not null)
                    {
                        snapshots.Add(new Domain.ZoneOccupancySnapshot(
                            Guid.NewGuid(),
                            run.EventId,
                            zone.ZoneId,
                            // El catálogo de Ticketing entrega EventStartsAtUtc con offset local
                            // (p.ej. +02:00); Npgsql rechaza escribir offsets != 0 en timestamptz.
                            virtualNowUtc.ToUniversalTime(),
                            updated.ConfirmedSold,
                            updated.CheckedIn));
                    }
                }

                if (snapshots.Count > 0)
                    await snapshotRepository.AddRangeAsync(snapshots, cancellationToken);
            }

            if (completed)
                _runs.TryRemove(run.EventId, out _);
        }
    }

    private sealed class SimulationRun
    {
        private readonly DateTimeOffset _startedAtUtc;
        private readonly double _virtualSecondsPerRealSecond;

        public Guid EventId { get; }
        public SimulationMode Mode { get; }
        public DateTimeOffset EventStartsAtUtc { get; }
        private TimeSpan Window { get; }
        public double TargetSoldPct { get; }
        public double NoShowPct { get; }

        public SimulationRun(
            Guid eventId,
            SimulationMode mode,
            DateTimeOffset eventStartsAtUtc,
            TimeSpan window,
            int durationSeconds,
            double targetSoldPct,
            double noShowPct)
        {
            EventId = eventId;
            Mode = mode;
            EventStartsAtUtc = eventStartsAtUtc;
            Window = window;
            TargetSoldPct = targetSoldPct;
            NoShowPct = noShowPct;

            _startedAtUtc = DateTimeOffset.UtcNow;
            _virtualSecondsPerRealSecond = window.TotalSeconds / Math.Max(1, durationSeconds);
        }

        public double Progress(DateTimeOffset nowUtc)
        {
            var elapsed = (nowUtc - _startedAtUtc).TotalSeconds * _virtualSecondsPerRealSecond;
            return Math.Clamp(elapsed / Window.TotalSeconds, 0, 1);
        }

        public DateTimeOffset VirtualNowUtc(DateTimeOffset nowUtc)
        {
            var elapsed = (nowUtc - _startedAtUtc).TotalSeconds * _virtualSecondsPerRealSecond;
            return EventStartsAtUtc - Window + TimeSpan.FromSeconds(elapsed);
        }
    }
}
