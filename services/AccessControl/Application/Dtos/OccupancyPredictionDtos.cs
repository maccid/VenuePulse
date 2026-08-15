namespace VenuePulse.AccessControl.Application;

public sealed record OccupancyPredictionPointDto(DateTimeOffset TimeUtc, double Progress, int Arrivals);

public sealed record OccupancyPredictionDto(
    Guid EventId,
    bool FitFromData,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset EventStartsAtUtc,
    DateTimeOffset NowUtc,
    int ConfirmedSold,
    int CheckedIn,
    int PredictedTotalArrivals,
    int PredictedNoShows,
    double NoShowRate,
    double? FitR2,
    IReadOnlyList<OccupancyPredictionPointDto> Historical,
    IReadOnlyList<OccupancyPredictionPointDto> Projected);
