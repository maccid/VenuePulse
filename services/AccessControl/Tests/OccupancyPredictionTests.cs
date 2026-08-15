namespace VenuePulse.AccessControl.Tests;

using FluentAssertions;
using VenuePulse.AccessControl.Application;
using VenuePulse.AccessControl.Application.Prediction;
using VenuePulse.AccessControl.Domain;

public class OccupancyPredictionTests
{
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid ZoneA = Guid.NewGuid();
    private static readonly Guid ZoneB = Guid.NewGuid();

    private static readonly DateTimeOffset EventStart = new(2026, 8, 15, 19, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowStart = EventStart - TimeSpan.FromHours(3);

    private static ObservedArrival Arrival(double progress, int arrivals) => new(progress, arrivals);

    [Fact]
    public void Ajusta_la_asintota_sobre_datos_sinteticos_limpios()
    {
        var observed = new[]
        {
            Arrival(0.2, 45),
            Arrival(0.4, 72),
            Arrival(0.6, 86),
            Arrival(0.8, 91),
            Arrival(0.9, 92),
        };

        var result = ArrivalForecast.Fit(observed, confirmedSold: 100);

        result.FitFromData.Should().BeTrue();
        result.PredictedTotalArrivals.Should().BeInRange(90, 94);
        result.NoShowRate.Should().BeApproximately(0.08, 0.02);
        result.FitR2.Should().NotBeNull();
        result.FitR2!.Value.Should().BeGreaterThan(0.99);
    }

    [Fact]
    public void Con_menos_de_dos_puntos_usa_no_show_asumido()
    {
        var result = ArrivalForecast.Fit([Arrival(0.5, 10)], confirmedSold: 100);

        result.FitFromData.Should().BeFalse();
        result.PredictedTotalArrivals.Should().Be(92);
        result.NoShowRate.Should().Be(ArrivalForecast.DefaultNoShowRate);
        result.FitR2.Should().BeNull();
    }

    [Fact]
    public void Sin_entradas_vendidas_no_hay_prediccion()
    {
        var result = ArrivalForecast.Fit([], confirmedSold: 0);

        result.PredictedTotalArrivals.Should().Be(0);
        result.NoShowRate.Should().Be(0);
        result.FitFromData.Should().BeFalse();
    }

    [Fact]
    public void La_asintota_nunca_supera_lo_vendido()
    {
        var observed = new[]
        {
            Arrival(0.05, 30),
            Arrival(0.1, 70),
        };

        var result = ArrivalForecast.Fit(observed, confirmedSold: 100);

        result.PredictedTotalArrivals.Should().BeLessThanOrEqualTo(100);
        result.NoShowRate.Should().BeGreaterThanOrEqualTo(0);
        result.PredictedTotalArrivals.Should().BeGreaterThanOrEqualTo(70);
    }

    [Fact]
    public async Task Servicio_devuelve_null_sin_historial()
    {
        var service = new OccupancyPredictionService(new FakeSnapshotRepository());

        var result = await service.PredictAsync(EventId, EventStart, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task En_fase_de_venta_aun_sin_llegadas_predice_con_no_show_asumido()
    {
        var repository = new FakeSnapshotRepository();
        repository.Snapshots.Add(Snapshot(ZoneA, WindowStart, confirmedSold: 60, checkedIn: 0));
        repository.Snapshots.Add(Snapshot(ZoneB, WindowStart, confirmedSold: 40, checkedIn: 0));
        repository.Snapshots.Add(Snapshot(ZoneA, EventStart.AddMinutes(-1), confirmedSold: 60, checkedIn: 0));
        repository.Snapshots.Add(Snapshot(ZoneB, EventStart.AddMinutes(-1), confirmedSold: 40, checkedIn: 0));

        var service = new OccupancyPredictionService(repository);
        var result = (await service.PredictAsync(EventId, EventStart, CancellationToken.None))!;

        result.FitFromData.Should().BeFalse();
        result.ConfirmedSold.Should().Be(100);
        result.CheckedIn.Should().Be(0);
        result.PredictedTotalArrivals.Should().Be(92);
        result.NoShowRate.Should().Be(ArrivalForecast.DefaultNoShowRate);
        result.Historical.Should().NotBeEmpty();
        result.Projected.Last().Arrivals.Should().Be(result.PredictedTotalArrivals);
    }

    [Fact]
    public async Task Agrega_las_zonas_y_proyecta_la_curva_hasta_el_evento()
    {
        var repository = new FakeSnapshotRepository();
        AddArrivalsSeries(repository, 60, 40, 92);

        var service = new OccupancyPredictionService(repository);
        var result = (await service.PredictAsync(EventId, EventStart, CancellationToken.None))!;

        result.FitFromData.Should().BeTrue();
        result.ConfirmedSold.Should().Be(100);
        result.CheckedIn.Should().Be(92);
        result.NowUtc.Should().Be(EventStart);
        result.PredictedTotalArrivals.Should().BeInRange(90, 94);
        result.PredictedNoShows.Should().BeInRange(6, 10);
        result.FitR2.Should().NotBeNull();
        result.FitR2!.Value.Should().BeGreaterThan(0.99);

        result.Historical.Select(p => p.Arrivals).Should().BeEquivalentTo(new[] { 45, 72, 86, 91, 92, 92 });
        result.Historical.Should().BeInAscendingOrder(p => p.TimeUtc);

        result.Projected[0].Progress.Should().Be(0);
        result.Projected[0].Arrivals.Should().Be(0);
        result.Projected[^1].Progress.Should().Be(1);
        result.Projected[^1].TimeUtc.Should().Be(EventStart);
        result.Projected[^1].Arrivals.Should().Be(result.PredictedTotalArrivals);
        result.Projected.Should().BeInAscendingOrder(p => p.Progress);
    }

    [Fact]
    public async Task Ignora_snapshots_fuera_de_la_ventana_de_llegada()
    {
        var repository = new FakeSnapshotRepository();
        AddArrivalsSeries(repository, 60, 40, 92);

        repository.Snapshots.Add(Snapshot(ZoneA, EventStart.AddHours(2), confirmedSold: 999, checkedIn: 0));
        repository.Snapshots.Add(Snapshot(ZoneB, EventStart.AddHours(2), confirmedSold: 999, checkedIn: 0));
        repository.Snapshots.Add(Snapshot(ZoneA, WindowStart.AddDays(-1), confirmedSold: 50, checkedIn: 0));

        var service = new OccupancyPredictionService(repository);
        var result = (await service.PredictAsync(EventId, EventStart, CancellationToken.None))!;

        result.NowUtc.Should().Be(EventStart);
        result.CheckedIn.Should().Be(92);
        result.ConfirmedSold.Should().Be(100);
        result.FitFromData.Should().BeTrue();
        result.FitR2.Should().NotBeNull();
        result.FitR2!.Value.Should().BeGreaterThan(0.99);
    }

    private static ZoneOccupancySnapshot Snapshot(Guid zoneId, DateTimeOffset timeUtc, int confirmedSold, int checkedIn) =>
        new(Guid.NewGuid(), EventId, zoneId, timeUtc, confirmedSold, checkedIn);

    private static void AddArrivalsSeries(FakeSnapshotRepository repository, int soldA, int soldB, int totalArrivals)
    {
        var series = new[]
        {
            (Time: WindowStart.AddMinutes(36), P: 0.2, Arrivals: 45),
            (Time: WindowStart.AddMinutes(72), P: 0.4, Arrivals: 72),
            (Time: WindowStart.AddMinutes(108), P: 0.6, Arrivals: 86),
            (Time: WindowStart.AddMinutes(144), P: 0.8, Arrivals: 91),
            (Time: WindowStart.AddMinutes(174), P: 0.9, Arrivals: 92),
            (Time: EventStart, P: 1.0, Arrivals: 92),
        };

        foreach (var (time, p, arrivals) in series)
        {
            var arrivalsA = (int)Math.Round(arrivals * (double)soldA / (soldA + soldB));
            repository.Snapshots.Add(Snapshot(ZoneA, time, soldA, arrivalsA));
            repository.Snapshots.Add(Snapshot(ZoneB, time, soldB, arrivals - arrivalsA));
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
}
