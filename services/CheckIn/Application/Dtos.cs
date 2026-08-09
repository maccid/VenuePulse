namespace VenuePulse.CheckIn.Application;

// EventId/ZoneId viajan aquí (no solo TicketId/Status) para que venue-ops pueda pintar un registro
// de actividad en vivo por zona sin tener que consultar cada entrada por separado.
public sealed record TicketStatusDto(Guid TicketId, Guid EventId, Guid ZoneId, string Status, string? RejectionReason);

public sealed record TicketSummaryDto(
    Guid TicketId,
    Guid EventId,
    Guid ZoneId,
    string Holder,
    string Status,
    DateTimeOffset CreatedAtUtc);
