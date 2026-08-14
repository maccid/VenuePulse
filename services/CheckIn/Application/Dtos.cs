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

// Estado de una línea (Ticket) dentro de un pedido — mismos campos que TicketStatusDto salvo que
// aquí sí viaja TicketTypeId/UnitPrice (el pedido los necesita para mostrar el desglose por línea,
// ver ADR 0028) y no EventId (ya está una sola vez en OrderStatusDto, no repetido por línea).
public sealed record OrderTicketStatusDto(
    Guid TicketId,
    Guid ZoneId,
    Guid TicketTypeId,
    decimal UnitPrice,
    string Status,
    string? RejectionReason,
    DateTimeOffset? ExpiresAtUtc);

// Estado agregado de un PurchaseOrder (ADR 0028) — RejectionReason aquí es el motivo real del
// pedido (precio/aforo), a diferencia del RejectionReason por-ticket que puede quedar con el
// literal ambiguo "EventCancelled" al reutilizar TicketPurchaseCancelled para compensar líneas
// hermanas (ver ADR 0028, "Consecuencias"). El frontend deja de mostrar el motivo por-ticket y
// solo muestra este.
public sealed record OrderStatusDto(
    Guid OrderId,
    Guid EventId,
    string Status,
    string? RejectionReason,
    IReadOnlyCollection<OrderTicketStatusDto> Tickets);

// Confirmed = Confirmed+CheckedIn (venta ya cerrada); Pending = AwaitingPayment+ProcessingPayment
// (todavía puede acabar en venta o en rechazo). Refunded/Rejected quedan fuera por construcción —
// ver ADR 0021: un reembolso baja los ingresos solo por dejar de sumar, sin necesidad de restarlo.
public sealed record EventRevenueDto(
    Guid EventId,
    decimal ConfirmedRevenue,
    int ConfirmedCount,
    decimal PendingRevenue,
    int PendingCount);
