namespace VenuePulse.AccessControl.Domain;

using VenuePulse.Core.Domain;

/// <summary>
/// Punto de una serie temporal de ocupación de una zona, generado por el motor de simulación
/// (Bloque 8). Es la materia prima del Bloque 11 (predicción): la curva de llegada y los
/// no-shows se modelan sobre estos datos que el propio sistema genera.
/// </summary>
public sealed class ZoneOccupancySnapshot : Entity<Guid>
{
    public Guid EventId { get; private set; }
    public Guid ZoneId { get; private set; }

    /// <summary>Tiempo simulado (no el reloj real) — por eso la curva "avanza" en segundos reales
    /// pero queda registrada sobre una línea temporal plausible (días/horas antes del evento).</summary>
    public DateTimeOffset RecordedAtUtc { get; private set; }

    public int ConfirmedSold { get; private set; }
    public int CheckedIn { get; private set; }

    private ZoneOccupancySnapshot()
    {
        // EF Core
    }

    public ZoneOccupancySnapshot(Guid id, Guid eventId, Guid zoneId, DateTimeOffset recordedAtUtc, int confirmedSold, int checkedIn) : base(id)
    {
        EventId = eventId;
        ZoneId = zoneId;
        RecordedAtUtc = recordedAtUtc;
        ConfirmedSold = confirmedSold;
        CheckedIn = checkedIn;
    }
}
