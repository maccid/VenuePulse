namespace VenuePulse.Ticketing.Application;

using VenuePulse.Core.Domain;
using VenuePulse.Ticketing.Domain;

public sealed record EventSummaryDto(
    Guid Id,
    string Name,
    string VenueName,
    DateTimeOffset StartsAtUtc,
    EventType Type,
    EventStatus Status,
    EventRelevance Relevance);

/// <summary>Alta de evento desde Operaciones (venue-ops) — VenueName no se pide, el recinto es
/// único en esta demo (Estadio Central, ver .ia/PRODUCT-VISION.md).</summary>
public sealed record CreateEventRequest(
    string Name,
    DateTimeOffset StartsAtUtc,
    EventType Type,
    EventRelevance Relevance);

// Price es el precio de catálogo (el que se guarda); BasePrice añade el multiplicador de
// relevancia (p. ej. x1.5 en eventos especiales, ver ADR 0019) — es el precio de referencia
// antes de la heurística de demanda. EffectivePrice/PriceTier son el resultado de aplicarle la
// heurística de demanda (Bloque 9, ver ADR 0016) — se recalculan en cada lectura, no se
// persisten, para que un cambio de heurística no obligue a migrar datos históricos.
public sealed record TicketTypeDto(
    Guid Id,
    Guid EventId,
    string Name,
    decimal Price,
    decimal BasePrice,
    string? Description,
    decimal EffectivePrice,
    string PriceTier);
