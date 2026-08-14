namespace VenuePulse.AccessControl.Application;

using VenuePulse.AccessControl.Application.Simulation;

/// <summary>
/// Arranque de un run de simulación para un evento. El presentador decide el modo, cuánto dura
/// la simulación en tiempo real (intensidad), el objetivo de venta y el % de no-shows. Venue-ops
/// trae <see cref="SimulationStartRequest.EventStartsAtUtc"/> del catálogo de Ticketing —
/// AccessControl no consulta la DB de otro servicio (bounded contexts separados).
/// </summary>
public sealed record SimulationStartRequest(
    SimulationMode Mode,
    DateTimeOffset EventStartsAtUtc,
    int DurationSeconds = 20,
    double TargetSoldPct = 0.85,
    double NoShowPct = 0.08);

/// <summary>Estado visible del run de simulación para el frontend (venue-ops).</summary>
public sealed record SimulationRunStatusDto(
    Guid EventId,
    SimulationMode Mode,
    bool Running,
    double Progress,
    DateTimeOffset VirtualNowUtc);
