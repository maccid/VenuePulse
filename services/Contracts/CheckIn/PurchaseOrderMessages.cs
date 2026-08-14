namespace VenuePulse.Core.Messaging.Contracts;

/// <summary>
/// Referencia a una línea de <c>PurchaseOrder</c> dentro de <see cref="PurchaseOrderRequested"/> —
/// solo lo que la <c>PurchaseOrderSaga</c> necesita para orquestar (ADR 0028): el
/// <c>TicketId</c> de la entrada hermana y su <c>ZoneId</c> (para poder publicar
/// <see cref="ReleaseConfirmedZoneSlot"/> al compensar una línea ya confirmada). El resto de la
/// línea (tipo de entrada, precio) ya vive en el propio <c>Ticket</c> — no se duplica aquí.
/// </summary>
public sealed record PurchaseOrderTicketRef(Guid TicketId, Guid ZoneId);

/// <summary>
/// Evento iniciador de la <c>PurchaseOrderSaga</c> (CorrelationId = <c>PurchaseOrder.Id</c>) — ver
/// ADR 0028. Publicado por <c>PlaceOrderCommandHandler</c> justo después de publicar, una por una,
/// las <see cref="TicketPurchaseRequested"/> de cada línea (mismo <c>GroupId</c> = este
/// <c>CorrelationId</c>), para que la Saga orquestadora sepa cuántas líneas hermanas debe esperar.
/// "<c>Id</c>" y no "EventId": <see cref="IntegrationEvent.EventId"/> ya es el Id del propio
/// mensaje, no el del <c>Event</c> de dominio al que pertenece el pedido (mismo motivo ya
/// documentado en <see cref="ZoneOccupancyChanged"/>).
/// </summary>
public sealed record PurchaseOrderRequested(Guid CorrelationId, Guid Id, IReadOnlyCollection<PurchaseOrderTicketRef> Tickets) : IntegrationEvent;

/// <summary>Publicado por la <c>PurchaseOrderSaga</c> cuando TODAS sus líneas reservaron aforo con
/// éxito — el pedido completo pasa a esperar un único paso de pago.</summary>
public sealed record PurchaseOrderAwaitingPayment(Guid CorrelationId) : IntegrationEvent;

/// <summary>Publicado por la Api cuando el cliente elige método de pago para el pedido completo
/// (<c>POST /api/orders/{id}/pay</c>) — la Saga lo reenvía como <c>TicketPaymentSubmitted</c> a
/// cada línea hermana, mismo <c>SimulateFailure</c> para todas (ver ADR 0028).</summary>
public sealed record PurchaseOrderPaymentSubmitted(Guid CorrelationId, bool SimulateFailure) : IntegrationEvent;

/// <summary>Publicado por la <c>PurchaseOrderSaga</c> cuando TODAS sus líneas confirmaron el
/// pago — el pedido completo queda vendido.</summary>
public sealed record PurchaseOrderCompleted(Guid CorrelationId) : IntegrationEvent;

/// <summary>Publicado por la <c>PurchaseOrderSaga</c> tras rechazar el pedido completo (precio
/// desactualizado, aforo insuficiente en cualquier línea, o fallo de pago en cualquier línea) —
/// mismo criterio todo-o-nada que ADR 0028 exige.</summary>
public sealed record PurchaseOrderRejected(Guid CorrelationId, string Reason) : IntegrationEvent;
