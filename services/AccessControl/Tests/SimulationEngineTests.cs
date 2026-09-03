namespace VenuePulse.AccessControl.Tests;

using FluentAssertions;
using VenuePulse.AccessControl.Application;
using VenuePulse.AccessControl.Application.Simulation;
using VenuePulse.AccessControl.Domain;
using VenuePulse.Core.Domain;

public class SimulationEngineTests
{
    private sealed class FakeZoneService : IZoneService
    {
        private readonly List<ZoneOccupancyDto> _zones;

        public FakeZoneService(params ZoneOccupancyDto[] zones) => _zones = zones.ToList();

        public int AppliedSales { get; private set; }
        public int AppliedCheckIns { get; private set; }

        public Task<IReadOnlyCollection<ZoneOccupancyDto>> GetAllOccupancyAsync(Guid? eventId, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<ZoneOccupancyDto> result = _zones.Where(z => z.EventId == eventId).ToList();
            return Task.FromResult(result);
        }

        public Task<ZoneOccupancyDto?> GetOccupancyAsync(Guid zoneId, CancellationToken cancellationToken) =>
            Task.FromResult(_zones.SingleOrDefault(z => z.ZoneId == zoneId));

        public Task<bool> TryReserveSlotAsync(Guid zoneId, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task ConfirmSlotAsync(Guid zoneId, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task ReleaseSlotAsync(Guid zoneId, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task CancelEventZonesAsync(Guid eventId, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task ReleaseConfirmedSlotAsync(Guid zoneId, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<int> RecordCheckInAsync(Guid zoneId, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task ProvisionZonesForEventAsync(Guid eventId, EventType eventType, CancellationToken cancellationToken) => throw new NotImplementedException();

        public async Task<ZoneOccupancyDto> ApplySimulatedSalesAsync(Guid zoneId, int quantity, CancellationToken cancellationToken)
        {
            AppliedSales += quantity;
            return await MutateAsync(zoneId, dto => dto with
            {
                ConfirmedSold = dto.ConfirmedSold + quantity,
                AvailableToSell = dto.AvailableToSell - quantity,
            }, cancellationToken);
        }

        public async Task<ZoneOccupancyDto> ApplySimulatedCheckInsAsync(Guid zoneId, int quantity, CancellationToken cancellationToken)
        {
            AppliedCheckIns += quantity;
            return await MutateAsync(zoneId, dto => dto with { CheckedIn = dto.CheckedIn + quantity }, cancellationToken);
        }

        public async Task ResetSimulationAsync(Guid eventId, CancellationToken cancellationToken)
        {
            for (var i = 0; i < _zones.Count; i++)
            {
                var zone = _zones[i];
                _zones[i] = zone with { AvailableToSell = zone.Capacity, ConfirmedSold = 0, CheckedIn = 0 };
            }

            await Task.CompletedTask;
        }

        public async Task ResetArrivalsSimulationAsync(Guid eventId, CancellationToken cancellationToken)
        {
            for (var i = 0; i < _zones.Count; i++)
            {
                var zone = _zones[i];
                _zones[i] = zone with { CheckedIn = 0 };
            }

            await Task.CompletedTask;
        }

        private async Task<ZoneOccupancyDto> MutateAsync(Guid zoneId, Func<ZoneOccupancyDto, ZoneOccupancyDto> mutate, CancellationToken cancellationToken)
        {
            var index = _zones.FindIndex(z => z.ZoneId == zoneId);
            var updated = mutate(_zones[index]);
            _zones[index] = updated;
            await Task.CompletedTask;
            return updated;
        }
    }

    private sealed class FakeSnapshotRepository : IOccupancySnapshotRepository
    {
        public List<ZoneOccupancySnapshot> Snapshots { get; } = [];

        public Task AddRangeAsync(IReadOnlyCollection<ZoneOccupancySnapshot> snapshots, CancellationToken cancellationToken)
        {
            Snapshots.AddRange(snapshots);
            return Task.CompletedTask;
        }

        public Task ClearForEventAsync(Guid eventId, CancellationToken cancellationToken)
        {
            Snapshots.RemoveAll(s => s.EventId == eventId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ZoneOccupancySnapshot>> GetForEventAsync(Guid eventId, CancellationToken cancellationToken)
        {
            IReadOnlyList<ZoneOccupancySnapshot> result = Snapshots
                .Where(s => s.EventId == eventId)
                .OrderBy(s => s.RecordedAtUtc)
                .ToList();
            return Task.FromResult(result);
        }
    }

    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid ZoneId = Guid.NewGuid();

    private static ZoneOccupancyDto CreateZone(int capacity = 100) =>
        new(ZoneId, EventId, "Puerta Norte", capacity, capacity, 0, 0, false);

    private static SimulationStartRequest SalesRequest(DateTimeOffset eventStartsAtUtc) =>
        new(SimulationMode.Sales, eventStartsAtUtc, DurationSeconds: 100, TargetSoldPct: 0.8, NoShowPct: 0.08);

    [Fact]
    public async Task Venta_simulada_avanza_hacia_el_objetivo_sin_superarlo()
    {
        var engine = new SimulationEngine();
        var zoneService = new FakeZoneService(CreateZone(capacity: 100));
        var snapshots = new FakeSnapshotRepository();

        var started = await engine.TryStartAsync(EventId, SalesRequest(DateTimeOffset.UtcNow.AddDays(7)), CancellationToken.None);
        started.Should().BeTrue();

        for (var i = 0; i < 5; i++)
        {
            var nowUtc = DateTimeOffset.UtcNow.AddSeconds(i * 21);
            await engine.AdvanceAsync(nowUtc, zoneService, snapshots, CancellationToken.None);
        }

        zoneService.AppliedSales.Should().BeGreaterThan(0);
        zoneService.AppliedSales.Should().BeLessThanOrEqualTo(80);
        snapshots.Snapshots.Should().NotBeEmpty();
        snapshots.Snapshots.All(s => s.ConfirmedSold <= 80).Should().BeTrue();
    }

    [Fact]
    public async Task No_se_puede_arrancar_dos_runs_para_el_mismo_evento()
    {
        var engine = new SimulationEngine();
        var zoneService = new FakeZoneService(CreateZone());
        var snapshots = new FakeSnapshotRepository();
        var request = SalesRequest(DateTimeOffset.UtcNow.AddDays(7));

        (await engine.TryStartAsync(EventId, request, CancellationToken.None)).Should().BeTrue();
        (await engine.TryStartAsync(EventId, request, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task El_run_se_elimina_al_completarse()
    {
        var engine = new SimulationEngine();
        var zoneService = new FakeZoneService(CreateZone(capacity: 100));
        var snapshots = new FakeSnapshotRepository();

        await engine.TryStartAsync(EventId, SalesRequest(DateTimeOffset.UtcNow.AddDays(7)), CancellationToken.None);

        await engine.AdvanceAsync(DateTimeOffset.UtcNow.AddSeconds(200), zoneService, snapshots, CancellationToken.None);

        engine.GetStatus(EventId).Should().BeNull();
        zoneService.AppliedSales.Should().Be(80);
    }

    [Fact]
    public async Task Stop_elimina_el_run_sin_aplicar_mas_ventas()
    {
        var engine = new SimulationEngine();
        var zoneService = new FakeZoneService(CreateZone(capacity: 100));
        var snapshots = new FakeSnapshotRepository();
        var request = SalesRequest(DateTimeOffset.UtcNow.AddDays(7));

        await engine.TryStartAsync(EventId, request, CancellationToken.None);
        engine.Stop(EventId).Should().BeTrue();

        await engine.AdvanceAsync(DateTimeOffset.UtcNow.AddSeconds(10), zoneService, snapshots, CancellationToken.None);

        zoneService.AppliedSales.Should().Be(0);
        engine.GetStatus(EventId).Should().BeNull();
    }

    [Fact]
    public async Task El_ultimo_snapshot_nunca_supera_el_inicio_del_evento()
    {
        var engine = new SimulationEngine();
        var zoneService = new FakeZoneService(CreateZone(capacity: 100));
        var snapshots = new FakeSnapshotRepository();
        var eventStartsAtUtc = DateTimeOffset.UtcNow.AddDays(7);

        await engine.TryStartAsync(EventId, SalesRequest(eventStartsAtUtc), CancellationToken.None);
        for (var i = 0; i < 4; i++)
            await engine.AdvanceAsync(DateTimeOffset.UtcNow.AddSeconds(i * 21), zoneService, snapshots, CancellationToken.None);
        await engine.AdvanceAsync(DateTimeOffset.UtcNow.AddSeconds(200), zoneService, snapshots, CancellationToken.None);

        snapshots.Snapshots.Should().NotBeEmpty();
        snapshots.Snapshots.Max(s => s.RecordedAtUtc).Should().BeBefore(eventStartsAtUtc);
        engine.GetStatus(EventId).Should().BeNull();
    }

    [Fact]
    public async Task Los_snapshots_se_escriben_en_utc()
    {
        var engine = new SimulationEngine();
        var zoneService = new FakeZoneService(CreateZone(capacity: 100));
        var snapshots = new FakeSnapshotRepository();
        var eventStartsAtUtc = new DateTimeOffset(2026, 8, 15, 19, 0, 0, TimeSpan.FromHours(2));

        await engine.TryStartAsync(EventId, SalesRequest(eventStartsAtUtc), CancellationToken.None);
        for (var i = 0; i < 3; i++)
            await engine.AdvanceAsync(DateTimeOffset.UtcNow.AddSeconds(i * 21), zoneService, snapshots, CancellationToken.None);

        snapshots.Snapshots.Should().NotBeEmpty();
        snapshots.Snapshots.All(s => s.RecordedAtUtc.Offset == TimeSpan.Zero).Should().BeTrue();
    }

    [Fact]
    public async Task Modo_arrivals_genera_no_shows_naturales()
    {
        var engine = new SimulationEngine();
        var zoneService = new FakeZoneService(CreateZone(capacity: 100));
        var snapshots = new FakeSnapshotRepository();

        await engine.TryStartAsync(EventId, SalesRequest(DateTimeOffset.UtcNow.AddDays(7)), CancellationToken.None);
        await engine.AdvanceAsync(DateTimeOffset.UtcNow.AddSeconds(200), zoneService, snapshots, CancellationToken.None);

        var arrivalsRequest = new SimulationStartRequest(
            SimulationMode.Arrivals,
            DateTimeOffset.UtcNow.AddDays(7),
            DurationSeconds: 100,
            TargetSoldPct: 0.8,
            NoShowPct: 0.08);
        await engine.TryStartAsync(EventId, arrivalsRequest, CancellationToken.None);
        await engine.AdvanceAsync(DateTimeOffset.UtcNow.AddSeconds(200), zoneService, snapshots, CancellationToken.None);

        var final = (await zoneService.GetOccupancyAsync(ZoneId, CancellationToken.None))!;
        final.CheckedIn.Should().Be(74);
        (final.ConfirmedSold - final.CheckedIn).Should().Be(6);
    }
}
