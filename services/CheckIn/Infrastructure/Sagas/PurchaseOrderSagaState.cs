namespace VenuePulse.CheckIn.Infrastructure.Sagas;

using MassTransit;
using VenuePulse.Core.Messaging.Contracts;

public sealed class PurchaseOrderSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }

    public int Version { get; set; }

    public string CurrentState { get; set; } = string.Empty;

    public Guid EventId { get; set; }

    public List<PurchaseOrderTicketRef> Tickets { get; set; } = [];

    public List<Guid> PendingTicketIds { get; set; } = [];

    public List<Guid> ReadyTicketIds { get; set; } = [];

    public List<Guid> ConfirmedTicketIds { get; set; } = [];

    public int ExpectedCount { get; set; }

    public string? RejectionReason { get; set; }
}
