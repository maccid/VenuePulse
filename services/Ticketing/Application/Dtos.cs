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
