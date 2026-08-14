namespace VenuePulse.CheckIn.Application;

// EventId/ZoneId viajan aquí (no solo TicketId/Status) para que venue-ops pueda pintar un registro
// de actividad en vivo por zona sin tener que consultar cada entrada por separado. ExpiresAtUtc
// sale de ReservationTimeoutTracker (nulo si no hay reserva activa vigente para este ticket, p.
// ej. ya Confirmed/Rejected) — permite pintar una cuenta atrás real en el frontend (ver ADR 0025).
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
