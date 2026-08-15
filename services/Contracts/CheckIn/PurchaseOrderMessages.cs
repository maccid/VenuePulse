namespace VenuePulse.Core.Messaging.Contracts;

public sealed record PurchaseOrderTicketRef(Guid TicketId, Guid ZoneId);

public sealed record PurchaseOrderRequested(Guid CorrelationId, Guid Id, IReadOnlyCollection<PurchaseOrderTicketRef> Tickets) : IntegrationEvent;

public sealed record PurchaseOrderAwaitingPayment(Guid CorrelationId) : IntegrationEvent;

public sealed record PurchaseOrderPaymentSubmitted(Guid CorrelationId, bool SimulateFailure) : IntegrationEvent;

public sealed record PurchaseOrderCompleted(Guid CorrelationId) : IntegrationEvent;

public sealed record PurchaseOrderRejected(Guid CorrelationId, string Reason) : IntegrationEvent;
