namespace VenuePulse.Core.Messaging.Contracts;

/// <summary>Evento iniciador de la TicketPurchaseSaga (CorrelationId = Id de la entrada).</summary>
public sealed record TicketPurchaseRequested(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

/// <summary>
/// Publicado por la propia Saga al transicionar a "AwaitingPayment" (no por quien reacciona a
/// ZoneSlotReserved) — así el Ticket en Postgres nunca se marca "esperando pago" antes de que la
/// Saga en Mongo haya terminado de verdad esa transición. Dos consumidores independientes del
/// mismo ZoneSlotReserved (la Saga y el proyector del Ticket) no tienen garantía de orden entre
/// sí; publicando esto DESPUÉS de persistir su propio estado, la Saga se convierte en la única
/// fuente de verdad de "cuándo" pasa esto.
/// </summary>
public sealed record TicketAwaitingPayment(Guid CorrelationId) : IntegrationEvent;

/// <summary>
/// Publicado por la Api cuando el cliente elige método de pago en el paso "AwaitingPayment" de
/// la Saga — SimulateFailure es el gatillo determinista de la demo (Bizum) para forzar el camino
/// de compensación en vivo; Tarjeta lo deja en false.
/// </summary>
public sealed record TicketPaymentSubmitted(Guid CorrelationId, bool SimulateFailure) : IntegrationEvent;

/// <summary>Publicado por la Saga al finalizar con éxito.</summary>
public sealed record TicketPurchaseConfirmed(Guid CorrelationId) : IntegrationEvent;

/// <summary>Publicado por la Saga tras una compensación (aforo lleno o "pago" simulado fallido).</summary>
public sealed record TicketPurchaseRejected(Guid CorrelationId, string Reason) : IntegrationEvent;

/// <summary>
/// Publicado por <c>ReservationTimeoutMonitor</c> cuando una reserva lleva demasiado tiempo en
/// <c>Reserving</c> sin respuesta de AccessControl — ver ADR 0010.
/// </summary>
public sealed record ReservationTimedOut(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

/// <summary>
/// Publicado por la propia Saga justo antes de esperar <c>Saga:PaymentProcessingDelaySeconds</c>
/// (ver ADR 0021) — ilumina el paso "Procesando pago" del timeline por SignalR mientras la Saga
/// resuelve confirmación/rechazo, en vez de que ese paso sea instantáneo e imperceptible.
/// </summary>
public sealed record TicketProcessingPayment(Guid CorrelationId) : IntegrationEvent;

/// <summary>
/// Publicado por <see cref="VenuePulse.Core.Messaging.Contracts.EventCancelled"/> → cascada en
/// CheckIn (ADR 0021) para una entrada ya <c>Confirmed</c> cuando su evento se cancela. Distinto
/// de <see cref="TicketPurchaseRejected"/>: una entrada reembolsada sí llegó a completarse, no es
/// un rechazo del flujo de compra.
/// </summary>
public sealed record TicketRefunded(Guid CorrelationId) : IntegrationEvent;

/// <summary>
/// Publicado por la cascada de <see cref="VenuePulse.Core.Messaging.Contracts.EventCancelled"/>
/// (ADR 0021) para una entrada todavía <c>Pending</c>/<c>AwaitingPayment</c> cuando su evento se
/// cancela — correlacionado por <c>CorrelationId</c> con la propia <c>TicketPurchaseSaga</c>, que
/// lo consume como una compensación más (mismo patrón que <see cref="ReservationTimedOut"/>).
/// </summary>
public sealed record TicketPurchaseCancelled(Guid CorrelationId) : IntegrationEvent;
