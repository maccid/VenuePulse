namespace VenuePulse.CheckIn.Application;

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
