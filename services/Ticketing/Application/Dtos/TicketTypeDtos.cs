namespace VenuePulse.Ticketing.Application;

public sealed record TicketTypeDto(
    Guid Id,
    Guid EventId,
    string Name,
    decimal Price,
    decimal BasePrice,
    string? Description,
    decimal EffectivePrice,
    string PriceTier);
