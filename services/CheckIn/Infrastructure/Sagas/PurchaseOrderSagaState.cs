namespace VenuePulse.CheckIn.Infrastructure.Sagas;

using MassTransit;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>
/// Estado de la <see cref="PurchaseOrderSaga"/> orquestadora, persistido en MongoDB (mismo motivo
/// que <see cref="TicketPurchaseSagaState"/>, ver ADR 0005/0028). <c>CorrelationId</c> ES el
/// <c>PurchaseOrder.Id</c> — el mismo valor que viaja como <c>GroupId</c> en los mensajes de la
/// <c>TicketPurchaseSaga</c> de cada línea hermana, por eso esta Saga correlaciona esos mensajes
/// por <c>GroupId</c> en vez de por su <c>CorrelationId</c> nativo (ver <see cref="PurchaseOrderSaga"/>).
/// </summary>
public sealed class PurchaseOrderSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }

    public int Version { get; set; }

    public string CurrentState { get; set; } = string.Empty;

    public Guid EventId { get; set; }

    public List<PurchaseOrderTicketRef> Tickets { get; set; } = [];

    /// <summary>Líneas cuyo desenlace del paso en curso todavía no se conoce — vacío marca "todas
    /// resueltas" en cada fase (reserva, pago, cancelación), ver <see cref="PurchaseOrderSaga"/>.</summary>
    public List<Guid> PendingTicketIds { get; set; } = [];

    /// <summary>
    /// Líneas que ya reservaron aforo con éxito (<see cref="TicketAwaitingPayment"/>) mientras el
    /// pedido seguía en <c>ReservingLines</c> — no forma parte del contrato original de este
    /// campo en el ADR 0028 (que solo listaba <see cref="PendingTicketIds"/> y
    /// <see cref="ConfirmedTicketIds"/>), añadido para poder compensar TAMBIÉN a estas líneas
    /// (liberar su aforo ya reservado vía <see cref="TicketPurchaseCancelled"/>) si una línea
    /// hermana falla al reservar más tarde — sin este campo, una línea ya lista que no está en
    /// <see cref="PendingTicketIds"/> se quedaría con el aforo reservado para siempre al rechazar
    /// el pedido, justo el bug de aforo huérfano que este ADR existe para resolver.
    /// </summary>
    public List<Guid> ReadyTicketIds { get; set; } = [];

    public List<Guid> ConfirmedTicketIds { get; set; } = [];

    public int ExpectedCount { get; set; }

    public string? RejectionReason { get; set; }
}
