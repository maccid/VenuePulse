namespace VenuePulse.Ticketing.Domain;

/// <summary>
/// Selector genérico de "relevancia" del evento (un derbi, un concierto de un artista muy
/// conocido...) — nunca nombres reales, ver .ia/PRODUCT-VISION.md. Multiplicador fijo, misma
/// heurística transparente que ya usa <see cref="DynamicPricing"/> (ADR 0016), pero aplicado
/// ANTES y FUERA de ella: la relevancia es un rasgo del evento, no de su demanda en tiempo real
/// (ver ADR 0019).
/// </summary>
public enum EventRelevance
{
    Standard,
    Special,
}

public static class EventRelevanceExtensions
{
    public static decimal Multiplier(this EventRelevance relevance) => relevance switch
    {
        EventRelevance.Standard => 1.0m,
        EventRelevance.Special => 1.5m,
        _ => throw new ArgumentOutOfRangeException(nameof(relevance), relevance, null),
    };
}
