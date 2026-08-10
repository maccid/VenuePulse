namespace VenuePulse.Ticketing.Domain;

/// <summary>
/// Ciclo de vida de negocio de un Event — decide si se puede seguir comprando, cancelando, o si
/// debe desaparecer del catálogo de fans (ver ADR 0019). Deliberadamente independiente del
/// `SimulationMode` (Sales/Arrivals) de AccessControl: ese motor solo decide qué curva de demo
/// genera, no el estado real del evento (ver ADR 0013/0016, mismo criterio de no acoplar).
/// </summary>
public enum EventStatus
{
    OnSale,
    SalesClosed,
    Cancelled,
}
