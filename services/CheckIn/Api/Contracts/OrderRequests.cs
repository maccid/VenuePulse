namespace VenuePulse.CheckIn.Api.Contracts;

public sealed record PlaceOrderLineDto(Guid TicketTypeId, Guid ZoneId, int Quantity, decimal UnitPrice);

public sealed record PlaceOrderRequest(Guid EventId, string Holder, IReadOnlyList<PlaceOrderLineDto> Lines);

public sealed record SubmitOrderPaymentRequest(string Method);
