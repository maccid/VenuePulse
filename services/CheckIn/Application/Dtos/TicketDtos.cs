namespace VenuePulse.CheckIn.Application;

public sealed record TicketStatusDto(
    Guid TicketId,
    Guid EventId,
    Guid ZoneId,
    string Status,
    string? RejectionReason,
    DateTimeOffset? ExpiresAtUtc);

public sealed record TicketSummaryDto(
    Guid TicketId,
    Guid EventId,
    Guid ZoneId,
    string Holder,
    string Status,
    DateTimeOffset CreatedAtUtc);
