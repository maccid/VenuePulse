namespace VenuePulse.CheckIn.Domain;

using VenuePulse.Core.Data;
using VenuePulse.Core.Domain;

public sealed class Ticket : Entity<Guid>, IAuditable
{
    public Guid EventId { get; private set; }
    public Guid TicketTypeId { get; private set; }
    public Guid ZoneId { get; private set; }
    public string Holder { get; private set; } = string.Empty;
    public TicketStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }

    public Guid? GroupId { get; private set; }

    public decimal Price { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ConfirmedAtUtc { get; private set; }
    public DateTimeOffset? CheckedInAtUtc { get; private set; }

    private Ticket()
    {
    }

    public Ticket(Guid id, Guid eventId, Guid ticketTypeId, Guid zoneId, string holder, decimal price) : base(id)
    {
        Holder = Guard.AgainstNullOrWhiteSpace(holder, nameof(holder), "La entrada necesita un titular.");
        Price = Guard.AgainstNonPositive(price, nameof(price), "La entrada necesita un precio positivo.");

        EventId = eventId;
        TicketTypeId = ticketTypeId;
        ZoneId = zoneId;
        Status = TicketStatus.Pending;
    }

    public void AssignToGroup(Guid groupId)
    {
        if (GroupId.HasValue)
            throw new InvalidOperationException("La entrada ya pertenece a un pedido.");

        GroupId = groupId;
    }

    public void MarkAwaitingPayment()
    {
        if (Status != TicketStatus.Pending)
            throw new InvalidOperationException($"Solo una entrada pendiente puede pasar a esperar pago (estado actual: {Status}).");

        Status = TicketStatus.AwaitingPayment;
    }

    public void MarkProcessingPayment()
    {
        if (Status != TicketStatus.AwaitingPayment)
            throw new InvalidOperationException($"Solo una entrada esperando pago puede pasar a procesando pago (estado actual: {Status}).");

        Status = TicketStatus.ProcessingPayment;
    }

    public void Confirm()
    {
        if (Status != TicketStatus.ProcessingPayment)
            throw new InvalidOperationException($"Solo una entrada procesando el pago se puede confirmar (estado actual: {Status}).");

        Status = TicketStatus.Confirmed;
        ConfirmedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Reject(string reason)
    {
        if (Status is not (TicketStatus.Pending or TicketStatus.AwaitingPayment or TicketStatus.ProcessingPayment))
            throw new InvalidOperationException($"Solo una entrada pendiente, esperando pago o procesando pago se puede rechazar (estado actual: {Status}).");

        Status = TicketStatus.Rejected;
        RejectionReason = reason;
    }

    public void MarkCheckedIn()
    {
        if (Status != TicketStatus.Confirmed)
            throw new InvalidOperationException($"Solo una entrada confirmada puede hacer check-in (estado actual: {Status}).");

        Status = TicketStatus.CheckedIn;
        CheckedInAtUtc = DateTimeOffset.UtcNow;
    }

    public void Refund()
    {
        if (Status != TicketStatus.Confirmed)
            throw new InvalidOperationException($"Solo una entrada confirmada se puede reembolsar (estado actual: {Status}).");

        Status = TicketStatus.Refunded;
    }
}
