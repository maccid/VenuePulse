namespace VenuePulse.Ticketing.Domain;

public static class DynamicPricing
{
    public const string TierBaja = "Demanda baja";
    public const string TierNormal = "Normal";
    public const string TierAlta = "Demanda alta";
    public const string TierUltimas = "Últimas entradas";

    public const int LastUnitsThreshold = 2;

    public static decimal? CalculateOccupancyPercentage(int totalCapacity, int totalAvailableToSell) =>
        totalCapacity <= 0 ? null : 1m - (decimal)totalAvailableToSell / totalCapacity;

    public static (decimal EffectivePrice, string Tier) Apply(decimal basePrice, int availableToSell, decimal? occupancyPercentage)
    {
        if (occupancyPercentage is null)
            return (basePrice, TierNormal);

        if (availableToSell <= LastUnitsThreshold)
            return (Round(basePrice * 1.30m), TierUltimas);

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
