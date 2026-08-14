namespace VenuePulse.CheckIn.Api.Contracts;

public sealed record PlaceOrderLineDto(Guid TicketTypeId, Guid ZoneId, int Quantity, decimal UnitPrice);

public sealed record PlaceOrderRequest(Guid EventId, string Holder, IReadOnlyList<PlaceOrderLineDto> Lines);

// Mismo criterio Bizum-falla que TicketsController.Pay (ver ADR 0028: un único paso de pago para
// el pedido completo, reenviado por la PurchaseOrderSaga a cada línea hermana).
public sealed record SubmitOrderPaymentRequest(string Method);
