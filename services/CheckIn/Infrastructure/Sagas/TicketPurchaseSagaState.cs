namespace VenuePulse.CheckIn.Infrastructure.Sagas;

using MassTransit;

/// <summary>
/// Estado de la Saga, persistido como documento en MongoDB (no en Postgres) — ver ADR 0005.
/// La forma de este documento cambia según el paso: no todos los campos existen en todos los
/// estados, lo cual encaja mejor como documento que como fila con columnas fijas.
/// </summary>
public sealed class TicketPurchaseSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }

    public int Version { get; set; }

    public string CurrentState { get; set; } = string.Empty;

    public Guid ZoneId { get; set; }

    public DateTimeOffset RequestedAtUtc { get; set; }

    public DateTimeOffset? SlotReservedAtUtc { get; set; }

    public string? RejectionReason { get; set; }
}
