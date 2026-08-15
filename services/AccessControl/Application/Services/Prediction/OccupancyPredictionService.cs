namespace VenuePulse.AccessControl.Application.Prediction;

public sealed class OccupancyPredictionService(IOccupancySnapshotRepository snapshotRepository) : IOccupancyPredictionService
{
    private static readonly TimeSpan ArrivalsWindow = TimeSpan.FromHours(3);

    private static readonly TimeSpan ProjectionStep = TimeSpan.FromMinutes(5);

    public async Task<OccupancyPredictionDto?> PredictAsync(
        Guid eventId,
        DateTimeOffset eventStartsAtUtc,
        CancellationToken cancellationToken)
    {
        var windowStartUtc = eventStartsAtUtc - ArrivalsWindow;

        var rows = (await snapshotRepository.GetForEventAsync(eventId, cancellationToken))
            .Where(snapshot => snapshot.RecordedAtUtc >= windowStartUtc && snapshot.RecordedAtUtc <= eventStartsAtUtc)
            .ToList();
        if (rows.Count == 0)
            return null;

        var byTime = rows
            .GroupBy(snapshot => snapshot.RecordedAtUtc)
            .OrderBy(group => group.Key)
            .Select(group => (
                Time: group.Key,
                ConfirmedSold: group.Sum(snapshot => snapshot.ConfirmedSold),
                CheckedIn: group.Sum(snapshot => snapshot.CheckedIn)))
            .ToList();

        var nowUtc = byTime[^1].Time;
        var confirmedSold = byTime[^1].ConfirmedSold;
        var checkedIn = byTime[^1].CheckedIn;

        double Progress(DateTimeOffset timeUtc)
        {
            var elapsed = (timeUtc - windowStartUtc).TotalSeconds;
            return Math.Clamp(elapsed / ArrivalsWindow.TotalSeconds, 0, 1);
        }

        var observed = byTime
            .Where(point => point.CheckedIn > 0)
            .Select(point => new ObservedArrival(Progress(point.Time), point.CheckedIn))
            .ToList();

        var forecast = ArrivalForecast.Fit(observed, confirmedSold);

        var historical = byTime
            .Select(point => new OccupancyPredictionPointDto(point.Time, Progress(point.Time), point.CheckedIn))
            .ToList();

        var projected = BuildProjection(windowStartUtc, eventStartsAtUtc, forecast.PredictedTotalArrivals);

        return new OccupancyPredictionDto(
            eventId,
            forecast.FitFromData,
            windowStartUtc,
            eventStartsAtUtc,
            nowUtc,
            confirmedSold,
            checkedIn,
            forecast.PredictedTotalArrivals,
            Math.Max(0, confirmedSold - forecast.PredictedTotalArrivals),
            forecast.NoShowRate,
            forecast.FitR2,
            historical,
            projected);
    }

    private static IReadOnlyList<OccupancyPredictionPointDto> BuildProjection(
        DateTimeOffset windowStartUtc,
        DateTimeOffset eventStartsAtUtc,
        int predictedTotalArrivals)
    {
        var window = eventStartsAtUtc - windowStartUtc;
        var points = new List<OccupancyPredictionPointDto>();

        for (var timeUtc = windowStartUtc; timeUtc <= eventStartsAtUtc; timeUtc += ProjectionStep)
        {
            var progress = Math.Clamp((timeUtc - windowStartUtc).TotalSeconds / window.TotalSeconds, 0, 1);
            points.Add(new OccupancyPredictionPointDto(
                timeUtc,
                progress,
                (int)Math.Round(predictedTotalArrivals * ArrivalForecast.Shape(progress))));
        }

        if (points.Count == 0 || points[^1].TimeUtc != eventStartsAtUtc)
            points.Add(new OccupancyPredictionPointDto(eventStartsAtUtc, 1, predictedTotalArrivals));

        return points;
    }
}
