namespace VenuePulse.CheckIn.Domain;

using VenuePulse.Core.Data;
using VenuePulse.Core.Domain;

public sealed class PurchaseOrder : Entity<Guid>, IAuditable
{
    public Guid EventId { get; private set; }
    public string Holder { get; private set; } = string.Empty;
    public PurchaseOrderStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    private PurchaseOrder()
    {
    }

    public PurchaseOrder(Guid id, Guid eventId, string holder) : base(id)
    {
        Holder = Guard.AgainstNullOrWhiteSpace(holder, nameof(holder), "El pedido necesita un titular.");

        EventId = eventId;
        Status = PurchaseOrderStatus.Reserving;
    }

    public void MarkAwaitingPayment()
    {
        if (Status != PurchaseOrderStatus.Reserving)
            throw new InvalidOperationException($"Solo un pedido reservando líneas puede pasar a esperar pago (estado actual: {Status}).");

        Status = PurchaseOrderStatus.AwaitingPayment;
    }

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

    public void Reject(string reason)
    {
        if (Status is not (PurchaseOrderStatus.Reserving or PurchaseOrderStatus.AwaitingPayment or PurchaseOrderStatus.Confirming))
            throw new InvalidOperationException($"Solo un pedido no finalizado se puede rechazar (estado actual: {Status}).");

        Status = PurchaseOrderStatus.Rejected;
        RejectionReason = reason;
    }
}
