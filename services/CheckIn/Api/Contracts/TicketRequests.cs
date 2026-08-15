namespace VenuePulse.CheckIn.Api.Contracts;

public sealed record PurchaseTicketRequest(Guid EventId, Guid TicketTypeId, Guid ZoneId, string Holder, decimal Price);

public sealed record SubmitPaymentRequest(string Method);
