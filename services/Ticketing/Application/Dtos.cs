namespace VenuePulse.Ticketing.Application;

using VenuePulse.Core.Domain;

public sealed record EventSummaryDto(
    Guid Id,
    string Name,
    string VenueName,
    DateTimeOffset StartsAtUtc,
    EventType Type);

// Price es el precio de catálogo (el que se guarda); EffectivePrice/PriceTier son el resultado de
// aplicarle la heurística de demanda (Bloque 9, ver ADR 0016) — se recalculan en cada lectura, no
// se persisten, para que un cambio de heurística no obligue a migrar datos históricos.
public sealed record TicketTypeDto(
    Guid Id,
    Guid EventId,
    string Name,
    decimal Price,
    string? Description,
    decimal EffectivePrice,
    string PriceTier);
