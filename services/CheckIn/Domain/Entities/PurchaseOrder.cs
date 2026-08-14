namespace VenuePulse.CheckIn.Domain;

using VenuePulse.Core.Data;
using VenuePulse.Core.Domain;

/// <summary>
/// Cabecera de un pedido multi-entrada (ADR 0028) — orquestado por la <c>PurchaseOrderSaga</c> por
/// encima de N <c>TicketPurchaseSaga</c> aisladas, una por cada <see cref="Ticket"/> de este pedido
/// (vinculadas por <see cref="Ticket.GroupId"/> = <see cref="Entity{TId}.Id"/> de este agregado).
/// Sin líneas propias: las líneas se derivan agrupando los propios <see cref="Ticket"/> por
/// <c>GroupId</c> cuando hace falta consultarlas (ver "Alternativas descartadas" del ADR 0028) —
/// este agregado solo guarda la cabecera (<see cref="EventId"/>/<see cref="Holder"/>/
/// <see cref="Status"/>/<see cref="RejectionReason"/>).
/// </summary>
public sealed class PurchaseOrder : Entity<Guid>, IAuditable
{
    public Guid EventId { get; private set; }
    public string Holder { get; private set; } = string.Empty;
    public PurchaseOrderStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }

    /// <summary>Rellenados por <see cref="AuditingSaveChangesInterceptor"/> al guardar — ver ADR 0011.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    private PurchaseOrder()
    {
        // EF Core
    }

    public PurchaseOrder(Guid id, Guid eventId, string holder) : base(id)
    {
        Holder = Guard.AgainstNullOrWhiteSpace(holder, nameof(holder), "El pedido necesita un titular.");

        EventId = eventId;
        Status = PurchaseOrderStatus.Reserving;
    }

    /// <summary>Todas las líneas reservaron aforo con éxito — el pedido completo espera un único
    /// paso de pago (ver <c>PurchaseOrderSaga</c>, estado <c>AwaitingPayment</c>).</summary>
    public void MarkAwaitingPayment()
    {
        if (Status != PurchaseOrderStatus.Reserving)
            throw new InvalidOperationException($"Solo un pedido reservando líneas puede pasar a esperar pago (estado actual: {Status}).");

        Status = PurchaseOrderStatus.AwaitingPayment;
    }

    /// <summary>El cliente ya eligió método de pago para el pedido completo — cada línea hermana
    /// resuelve su propio pago por separado (ver <c>PurchaseOrderSaga</c>, estado
    /// <c>ConfirmingLines</c>).</summary>
    public void MarkConfirming()
    {
        if (Status != PurchaseOrderStatus.AwaitingPayment)
            throw new InvalidOperationException($"Solo un pedido esperando pago puede pasar a confirmando líneas (estado actual: {Status}).");

        Status = PurchaseOrderStatus.Confirming;
    }

    public void Complete()
    {
        if (Status != PurchaseOrderStatus.Confirming)
            throw new InvalidOperationException($"Solo un pedido confirmando líneas se puede completar (estado actual: {Status}).");

        Status = PurchaseOrderStatus.Completed;
    }

    /// <summary>Rechazo todo-o-nada del pedido completo (ver ADR 0028) — válido desde cualquier
    /// estado no terminal: precio desactualizado o aforo insuficiente en cualquier línea
    /// (<c>Reserving</c>), o fallo de pago en cualquier línea (<c>Confirming</c>).</summary>
    public void Reject(string reason)
    {
        if (Status is not (PurchaseOrderStatus.Reserving or PurchaseOrderStatus.AwaitingPayment or PurchaseOrderStatus.Confirming))
            throw new InvalidOperationException($"Solo un pedido no finalizado se puede rechazar (estado actual: {Status}).");

        Status = PurchaseOrderStatus.Rejected;
        RejectionReason = reason;
    }
}
