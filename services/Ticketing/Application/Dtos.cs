namespace VenuePulse.Ticketing.Application;

public sealed record EventSummaryDto(
    Guid Id,
    string Name,
    string VenueName,
    DateTimeOffset StartsAtUtc);

public sealed record TicketTypeDto(
    Guid Id,
    Guid EventId,
    string Name,
    decimal Price,
    string? Description);
