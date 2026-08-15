namespace VenuePulse.CheckIn.Application;

public sealed record OrderTicketStatusDto(
    Guid TicketId,
    Guid ZoneId,
    Guid TicketTypeId,
    decimal UnitPrice,
    string Status,
    string? RejectionReason,
    DateTimeOffset? ExpiresAtUtc);

public sealed record OrderStatusDto(
    Guid OrderId,
    Guid EventId,
    string Status,
    string? RejectionReason,
    IReadOnlyCollection<OrderTicketStatusDto> Tickets);
