namespace VenuePulse.AccessControl.Application.Simulation;

using System.Collections.Concurrent;

public interface ISimulationEngine
{
    Task<bool> TryStartAsync(Guid eventId, SimulationStartRequest request, CancellationToken cancellationToken);

    bool Stop(Guid eventId);

    SimulationRunStatusDto? GetStatus(Guid eventId);

    Task AdvanceAsync(
        DateTimeOffset nowUtc,
        IZoneService zoneService,
        IOccupancySnapshotRepository snapshotRepository,
        CancellationToken cancellationToken);
}

public sealed class SimulationEngine : ISimulationEngine
{
    private static readonly TimeSpan SalesWindow = TimeSpan.FromDays(7);

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
