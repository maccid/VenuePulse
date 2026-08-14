namespace VenuePulse.Ticketing.Infrastructure;

/// <summary>
/// Fila de proyección, no un agregado de dominio: sin invariantes propias, solo el último valor
/// conocido de una zona de AccessControl (ver <see cref="Application.IZoneDemandRepository"/>).
/// Por eso vive en Infrastructure y no en Domain — nada que razonar salvo "último dato recibido".
/// </summary>
public sealed class ZoneDemandSnapshot
{
    public Guid ZoneId { get; set; }
    public Guid EventId { get; set; }
    public int Capacity { get; set; }
    public int AvailableToSell { get; set; }
    public int ConfirmedSold { get; set; }
    public int CheckedIn { get; set; }
}
