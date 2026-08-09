namespace VenuePulse.Ticketing.Domain;

/// <summary>
/// Precio ajustado por demanda con heurísticas transparentes en escalones fijos (nunca ML, ver
/// .ia/PRODUCT-VISION.md). La ocupación se agrega a nivel de <c>Event</c>, no de zona/sector:
/// <c>Zone</c> (AccessControl) y <c>TicketType</c> (Ticketing) son conceptos de bounded contexts
/// distintos sin relación 1:1 hoy — solo comparten <c>EventId</c> — así que un tipo de entrada no
/// puede anclarse a "su" zona concreta (ver ADR 0016).
/// </summary>
public static class DynamicPricing
{
    public const string TierBaja = "Demanda baja";
    public const string TierNormal = "Normal";
    public const string TierAlta = "Demanda alta";
    public const string TierUltimas = "Últimas entradas";

    /// <summary>
    /// Ocupación vendida (reservas + confirmadas) sobre el aforo total del evento, agregando
    /// todas sus zonas conocidas. Null si el evento todavía no tiene ninguna zona con datos (antes
    /// del primer aviso de aforo) — no hay demanda que medir todavía.
    /// </summary>
    public static decimal? CalculateOccupancyPercentage(int totalCapacity, int totalAvailableToSell) =>
        totalCapacity <= 0 ? null : 1m - (decimal)totalAvailableToSell / totalCapacity;

    public static (decimal EffectivePrice, string Tier) Apply(decimal basePrice, decimal? occupancyPercentage)
    {
        if (occupancyPercentage is null)
            return (basePrice, TierNormal);

        return occupancyPercentage switch
        {
            < 0.40m => (Round(basePrice * 0.90m), TierBaja),
            < 0.70m => (basePrice, TierNormal),
            < 0.90m => (Round(basePrice * 1.15m), TierAlta),
            _ => (Round(basePrice * 1.30m), TierUltimas),
        };
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
