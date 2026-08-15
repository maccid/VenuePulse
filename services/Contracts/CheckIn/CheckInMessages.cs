namespace VenuePulse.Core.Messaging.Contracts;

public sealed record TicketPurchaseRequested(Guid CorrelationId, Guid ZoneId, Guid? GroupId = null) : IntegrationEvent;

public sealed record TicketAwaitingPayment(Guid CorrelationId, Guid? GroupId = null) : IntegrationEvent;

public sealed record TicketPaymentSubmitted(Guid CorrelationId, bool SimulateFailure) : IntegrationEvent;

public sealed record TicketPurchaseConfirmed(Guid CorrelationId, Guid? GroupId = null) : IntegrationEvent;

public sealed record TicketPurchaseRejected(Guid CorrelationId, string Reason, Guid? GroupId = null) : IntegrationEvent;

public sealed record ReservationTimedOut(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record TicketProcessingPayment(Guid CorrelationId) : IntegrationEvent;

public sealed record TicketRefunded(Guid CorrelationId) : IntegrationEvent;

public sealed record TicketPurchaseCancelled(Guid CorrelationId, string Reason = "EventCancelled") : IntegrationEvent;
