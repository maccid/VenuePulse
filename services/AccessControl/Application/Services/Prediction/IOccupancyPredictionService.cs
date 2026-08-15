namespace VenuePulse.AccessControl.Application.Prediction;

public interface IOccupancyPredictionService
{
    Task<OccupancyPredictionDto?> PredictAsync(Guid eventId, DateTimeOffset eventStartsAtUtc, CancellationToken cancellationToken);
}
