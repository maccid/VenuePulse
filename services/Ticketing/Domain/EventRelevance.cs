namespace VenuePulse.Ticketing.Domain;

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
