namespace VenuePulse.AccessControl.Application;

/// <summary>Estado visible del run de simulación para el frontend (venue-ops).</summary>
public sealed record SimulationRunStatusDto(
    Guid EventId,
    SimulationMode Mode,
    bool Running,
    double Progress,
    DateTimeOffset VirtualNowUtc);
