namespace VenuePulse.Ticketing.Application;

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
