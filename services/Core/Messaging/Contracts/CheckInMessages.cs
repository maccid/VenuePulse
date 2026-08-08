namespace VenuePulse.Core.Messaging.Contracts;

/// <summary>
/// Evento iniciador de la TicketPurchaseSaga (CorrelationId = Id de la entrada). SimulateFailure
/// es el gatillo determinista de la demo para forzar el camino de compensación en vivo.
/// </summary>
public sealed record TicketPurchaseRequested(Guid CorrelationId, Guid ZoneId, bool SimulateFailure) : IntegrationEvent;

/// <summary>Publicado por la Saga al finalizar con éxito.</summary>
public sealed record TicketPurchaseConfirmed(Guid CorrelationId) : IntegrationEvent;

/// <summary>Publicado por la Saga tras una compensación (aforo lleno o "pago" simulado fallido).</summary>
public sealed record TicketPurchaseRejected(Guid CorrelationId, string Reason) : IntegrationEvent;

/// <summary>
/// Publicado por <c>ReservationTimeoutMonitor</c> cuando una reserva lleva demasiado tiempo en
/// <c>Reserving</c> sin respuesta de AccessControl — ver ADR 0010.
/// </summary>
public sealed record ReservationTimedOut(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;
