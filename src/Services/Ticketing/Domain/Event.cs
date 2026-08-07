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

    private readonly List<TicketType> _ticketTypes = [];
    public IReadOnlyCollection<TicketType> TicketTypes => _ticketTypes.AsReadOnly();

    private Event()
    {
        // EF Core
    }

    public Event(Guid id, string name, string venueName, DateTimeOffset startsAtUtc) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El evento necesita un nombre.", nameof(name));
        if (string.IsNullOrWhiteSpace(venueName))
            throw new ArgumentException("El evento necesita un recinto.", nameof(venueName));

        Name = name;
        VenueName = venueName;
        StartsAtUtc = startsAtUtc;
    }

    public TicketType AddTicketType(Guid id, string name, decimal price, string? description)
    {
        if (price <= 0)
            throw new ArgumentException("El precio de una entrada debe ser positivo.", nameof(price));

        var ticketType = new TicketType(id, Id, name, price, description);
        _ticketTypes.Add(ticketType);
        return ticketType;
    }
}
