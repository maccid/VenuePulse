namespace VenuePulse.AccessControl.Application.Prediction;

/// <summary>
/// Predicción de ocupación de un evento (Bloque 11). Application Service simple, sin CQRS (ver
/// ADR 0003): lee la serie temporal de <see cref="ZoneOccupancySnapshot"/> que genera el simulador
/// (Bloque 8), la agrega a nivel de evento y encaja el modelo de <see cref="ArrivalForecast"/>.
/// Devuelve null si el evento aún no tiene snapshots (404 en el endpoint).
/// </summary>
public interface IOccupancyPredictionService
{
    /// <summary>Predice la curva de llegada y los no-shows de un evento. <paramref name="eventStartsAtUtc"/>
    /// define la ventana de llegada (las 3 horas previas); lo trae venue-ops del catálogo de
    /// Ticketing — AccessControl no consulta la DB de otro servicio (bounded contexts).</summary>
    Task<OccupancyPredictionDto?> PredictAsync(Guid eventId, DateTimeOffset eventStartsAtUtc, CancellationToken cancellationToken);
}
