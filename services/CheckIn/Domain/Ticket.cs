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

    /// <summary>
    /// Precio efectivo que el cliente ya vio en el popup de compra (Bloque 14), fijado en el
    /// momento de la compra e inmutable después — los ingresos no varían retroactivamente si el
    /// precio dinámico de <c>Ticketing</c> cambia más tarde (ver ADR 0021, sección 5 del plan). El
    /// cliente lo envía sin revalidar contra <c>Ticketing</c>, riesgo aceptado documentado en el ADR.
    /// </summary>
    public decimal Price { get; private set; }

    /// <summary>Rellenados por <see cref="AuditingSaveChangesInterceptor"/> al guardar — ver ADR 0011.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ConfirmedAtUtc { get; private set; }
    public DateTimeOffset? CheckedInAtUtc { get; private set; }

    private Ticket()
    {
        // EF Core
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

    /// <summary>El aforo ya está reservado de verdad (ver TicketPurchaseSaga) — a partir de aquí
    /// la entrada espera a que el cliente elija método de pago.</summary>
    public void MarkAwaitingPayment()
    {
        if (Status != TicketStatus.Pending)
            throw new InvalidOperationException($"Solo una entrada pendiente puede pasar a esperar pago (estado actual: {Status}).");

        Status = TicketStatus.AwaitingPayment;
    }

    /// <summary>La Saga ya sabe qué método de pago eligió el cliente y empieza a resolver el
    /// resultado tras el delay configurable de "Procesando pago" (ver ADR 0021).</summary>
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

    /// <summary>Solo una entrada ya vendida (<see cref="TicketStatus.Confirmed"/>) puede
    /// reembolsarse — ver ADR 0021 sobre por qué es un estado terminal distinto de
    /// <see cref="TicketStatus.Rejected"/>.</summary>
    public void Refund()
    {
        if (Status != TicketStatus.Confirmed)
            throw new InvalidOperationException($"Solo una entrada confirmada se puede reembolsar (estado actual: {Status}).");

        Status = TicketStatus.Refunded;
    }
}
