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

// Confirmed = Confirmed+CheckedIn (venta ya cerrada); Pending = AwaitingPayment+ProcessingPayment
// (todavía puede acabar en venta o en rechazo). Refunded/Rejected quedan fuera por construcción —
// ver ADR 0021: un reembolso baja los ingresos solo por dejar de sumar, sin necesidad de restarlo.
public sealed record EventRevenueDto(
    Guid EventId,
    decimal ConfirmedRevenue,
    int ConfirmedCount,
    decimal PendingRevenue,
    int PendingCount);
