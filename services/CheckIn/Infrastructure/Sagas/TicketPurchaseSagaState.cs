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

    /// <summary>
    /// <c>PurchaseOrder.Id</c> cuando esta entrada nace como línea de un pedido multi-entrada (ver
    /// ADR 0028) — propagado desde <see cref="TicketPurchaseRequested"/>, nulo en una compra suelta
    /// (pre-ADR 0028). Solo se reenvía en los eventos publicados por esta Saga para que la
    /// <c>PurchaseOrderSaga</c> orquestadora pueda correlacionar esta línea con su pedido, sin que
    /// esta Saga sepa nada de pedidos.
    /// </summary>
    public Guid? GroupId { get; set; }

    public DateTimeOffset RequestedAtUtc { get; set; }

    public DateTimeOffset? SlotReservedAtUtc { get; set; }

    public string? RejectionReason { get; set; }
}
