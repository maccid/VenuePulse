namespace VenuePulse.AccessControl.Application;

using VenuePulse.AccessControl.Application.Simulation;

public sealed record SimulationStartRequest(
    SimulationMode Mode,
    DateTimeOffset EventStartsAtUtc,
    int DurationSeconds = 20,
    double TargetSoldPct = 0.85,
    double NoShowPct = 0.08);

public sealed record SimulationRunStatusDto(
    Guid EventId,
    SimulationMode Mode,
    bool Running,
    double Progress,
    DateTimeOffset VirtualNowUtc);
