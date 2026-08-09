namespace VenuePulse.Ticketing.Application;

/// <summary>
/// Copia local (proyección) del aforo de las zonas de AccessControl, alimentada por
/// <c>ZoneOccupancyChanged</c> — Ticketing no puede leer la base de datos de otro servicio (ver
/// .ia/RULES.md), así que mantiene su propia vista de solo lo que necesita para el precio dinámico.
/// </summary>
public interface IZoneDemandRepository
{
    Task UpsertAsync(
        Guid zoneId,
        Guid eventId,
        int capacity,
        int availableToSell,
        int confirmedSold,
        int checkedIn,
        CancellationToken cancellationToken);

    /// <summary>Ocupación agregada de todas las zonas conocidas del evento, o null si aún no ha
    /// llegado ningún aviso de aforo para él.</summary>
    Task<decimal?> GetOccupancyPercentageAsync(Guid eventId, CancellationToken cancellationToken);
}
