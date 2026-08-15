namespace VenuePulse.CheckIn.Infrastructure.Sagas;

using MassTransit;

public sealed class TicketPurchaseSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }

    public int Version { get; set; }

    public string CurrentState { get; set; } = string.Empty;

    public Guid ZoneId { get; set; }

    public Guid? GroupId { get; set; }

    public DateTimeOffset RequestedAtUtc { get; set; }

    public DateTimeOffset? SlotReservedAtUtc { get; set; }

    public string? RejectionReason { get; set; }
}
