namespace VenuePulse.CheckIn.Domain;

using VenuePulse.Core.Data;
using VenuePulse.Core.Domain;

/// <summary>
/// Único agregado con orquestación real (vía TicketPurchaseSaga) y consistencia eventual con
/// AccessControl. Aquí sí hay asimetría lectura/escritura real, por eso este es el único
/// servicio con CQRS (ver ADR 0003).
/// </summary>
public sealed class Ticket : Entity<Guid>, IAuditable
{
    public Guid EventId { get; private set; }
    public Guid TicketTypeId { get; private set; }
    public Guid ZoneId { get; private set; }
    public string Holder { get; private set; } = string.Empty;
    public TicketStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }

    /// <summary>Rellenados por <see cref="AuditingSaveChangesInterceptor"/> al guardar — ver ADR 0011.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ConfirmedAtUtc { get; private set; }
    public DateTimeOffset? CheckedInAtUtc { get; private set; }

    private Ticket()
    {
        // EF Core
    }

    public Ticket(Guid id, Guid eventId, Guid ticketTypeId, Guid zoneId, string holder) : base(id)
    {
        Holder = Guard.AgainstNullOrWhiteSpace(holder, nameof(holder), "La entrada necesita un titular.");

        EventId = eventId;
        TicketTypeId = ticketTypeId;
        ZoneId = zoneId;
        Status = TicketStatus.Pending;
    }

    public void Confirm()
    {
        if (Status != TicketStatus.Pending)
            throw new InvalidOperationException($"Solo una entrada pendiente se puede confirmar (estado actual: {Status}).");

        Status = TicketStatus.Confirmed;
        ConfirmedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Reject(string reason)
    {
        if (Status != TicketStatus.Pending)
            throw new InvalidOperationException($"Solo una entrada pendiente se puede rechazar (estado actual: {Status}).");

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
}
