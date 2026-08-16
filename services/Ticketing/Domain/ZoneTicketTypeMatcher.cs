namespace VenuePulse.Ticketing.Domain;

/// <summary>Empareja una zona con su TicketType por nombre — mismo criterio que
/// ui/venue-core/src/domain/zoneTicketType.ts (matchTicketType), para que el precio dinámico
/// por zona (ADR 0041) y la fusión visual del frontend nunca diverjan.</summary>
public static class ZoneTicketTypeMatcher
{
    public static TicketType? Match(string zoneName, IReadOnlyList<TicketType> ticketTypes)
    {
        if (ticketTypes.Count == 0)
            return null;

        var exactMatch = ticketTypes.FirstOrDefault(t => t.Name == zoneName);
        if (exactMatch is not null)
            return exactMatch;

        var longestContainedMatch = ticketTypes
            .Where(t => zoneName.Contains(t.Name, StringComparison.Ordinal))
            .OrderByDescending(t => t.Name.Length)
            .FirstOrDefault();
        if (longestContainedMatch is not null)
            return longestContainedMatch;

        return ticketTypes.FirstOrDefault(t => t.Name == "General") ?? ticketTypes[0];
    }
}
