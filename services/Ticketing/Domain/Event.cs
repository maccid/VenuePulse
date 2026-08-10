namespace VenuePulse.Ticketing.Domain;

using VenuePulse.Core.Domain;

/// <summary>
/// Catálogo: un partido/concierto y sus tipos de entrada. Cambia poco (alta esporádica de
/// eventos, consultas frecuentes) — por eso el servicio no necesita CQRS, ver ADR 0003.
/// </summary>
public sealed class Event : Entity<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string VenueName { get; private set; } = string.Empty;
    public DateTimeOffset StartsAtUtc { get; private set; }
    public EventType Type { get; private set; }
    public EventStatus Status { get; private set; }
    public EventRelevance Relevance { get; private set; }

    private readonly List<TicketType> _ticketTypes = [];
    public IReadOnlyCollection<TicketType> TicketTypes => _ticketTypes.AsReadOnly();

    private Event()
    {
        // EF Core
    }

    public Event(
        Guid id,
        string name,
        string venueName,
        DateTimeOffset startsAtUtc,
        EventType type = EventType.Match,
        EventRelevance relevance = EventRelevance.Standard) : base(id)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name), "El evento necesita un nombre.");
        VenueName = Guard.AgainstNullOrWhiteSpace(venueName, nameof(venueName), "El evento necesita un recinto.");
        StartsAtUtc = startsAtUtc;
        Type = type;
        Relevance = relevance;
        Status = EventStatus.OnSale;
    }

    public TicketType AddTicketType(Guid id, string name, decimal price, string? description)
    {
        Guard.AgainstNonPositive(price, nameof(price), "El precio de una entrada debe ser positivo.");

        var ticketType = new TicketType(id, Id, name, price, description);
        _ticketTypes.Add(ticketType);
        return ticketType;
    }

    /// <summary>Deja de admitir nuevas compras — no se sincroniza con el motor de simulación de
    /// AccessControl (Sales/Arrivals), son conceptos deliberadamente distintos (ver ADR 0019).</summary>
    public void CloseSales()
    {
        if (Status != EventStatus.OnSale)
            throw new InvalidOperationException($"Solo se puede cerrar la venta de un evento en venta (estado actual: {Status}).");

        Status = EventStatus.SalesClosed;
    }

    /// <summary>Solo válido mientras el evento sigue en venta — así ninguna entrada afectada
    /// puede estar nunca en CheckedIn, y el reembolso (CheckIn) no necesita contemplar ese caso.</summary>
    public void Cancel()
    {
        if (Status != EventStatus.OnSale)
            throw new InvalidOperationException($"Solo se puede cancelar un evento en venta (estado actual: {Status}).");

        Status = EventStatus.Cancelled;
    }
}
