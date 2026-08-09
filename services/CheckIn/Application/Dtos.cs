namespace VenuePulse.CheckIn.Application;

public sealed record TicketStatusDto(Guid TicketId, string Status, string? RejectionReason);

public sealed record TicketSummaryDto(
    Guid TicketId,
    Guid EventId,
    Guid ZoneId,
    string Holder,
    string Status,
    DateTimeOffset CreatedAtUtc);
