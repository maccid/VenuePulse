namespace VenuePulse.Ticketing.Application;

using VenuePulse.Core.Domain;
using VenuePulse.Ticketing.Domain;

public sealed record EventSummaryDto(
    Guid Id,
    string Name,
    string VenueName,
    DateTimeOffset StartsAtUtc,
    EventType Type,
    EventStatus Status,
    EventRelevance Relevance);

/// <summary>Alta de evento desde Operaciones (venue-ops) — VenueName no se pide, el recinto es
/// único en esta demo (Estadio Central, ver .ia/PRODUCT-VISION.md).</summary>
public sealed record CreateEventRequest(
    string Name,
    DateTimeOffset StartsAtUtc,
    EventType Type,
    EventRelevance Relevance);
