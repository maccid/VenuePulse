namespace VenuePulse.Ticketing.Domain;

using VenuePulse.Core.Domain;

public sealed class TicketType : Entity<Guid>
{
    public Guid EventId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public string? Description { get; private set; }

    private TicketType()
    {
        // EF Core
    }

    internal TicketType(Guid id, Guid eventId, string name, decimal price, string? description) : base(id)
    {
        EventId = eventId;
        Name = name;
        Price = price;
        Description = description;
    }
}
