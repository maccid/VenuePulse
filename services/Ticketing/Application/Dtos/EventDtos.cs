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

public sealed record CreateEventRequest(
    string Name,
    DateTimeOffset StartsAtUtc,
    EventType Type,
    EventRelevance Relevance);
