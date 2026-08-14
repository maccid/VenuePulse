namespace VenuePulse.CheckIn.Api.Contracts;

public sealed record PurchaseTicketRequest(Guid EventId, Guid TicketTypeId, Guid ZoneId, string Holder, decimal Price);

// "Card" | "Bizum" — Bizum es el gatillo determinista de la demo para forzar la compensación en
// vivo (ver TicketPaymentSubmitted); cualquier otro valor se trata como pago exitoso.
public sealed record SubmitPaymentRequest(string Method);
