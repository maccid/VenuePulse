namespace VenuePulse.AccessControl.Application;

/// <summary>Punto de la curva de llegada (histórica o proyectada): instante (tiempo simulado) y
/// personas que han pasado el torniquete en ese momento.</summary>
public sealed record OccupancyPredictionPointDto(DateTimeOffset TimeUtc, double Progress, int Arrivals);

/// <summary>
/// Predicción de ocupación de un evento (Bloque 11): "¿cuántos pasarán el torniquete y cuándo?".
/// Agrega la serie temporal del simulador a nivel de evento y proyecta la curva de llegada con la
/// asíntota ajustada por mínimos cuadrados (ver <see cref="ArrivalForecast"/>).
/// </summary>
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
