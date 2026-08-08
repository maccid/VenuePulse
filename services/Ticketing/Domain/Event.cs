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
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name), "El evento necesita un nombre.");
        VenueName = Guard.AgainstNullOrWhiteSpace(venueName, nameof(venueName), "El evento necesita un recinto.");
        StartsAtUtc = startsAtUtc;
    }

    public TicketType AddTicketType(Guid id, string name, decimal price, string? description)
    {
        Guard.AgainstNonPositive(price, nameof(price), "El precio de una entrada debe ser positivo.");

        var ticketType = new TicketType(id, Id, name, price, description);
        _ticketTypes.Add(ticketType);
        return ticketType;
    }
}
