namespace VenuePulse.AccessControl.Application;

public sealed record ZoneOccupancyDto(
    Guid ZoneId,
    Guid EventId,
    string Name,
    int Capacity,
    int AvailableToSell,
    int ConfirmedSold,
    int CheckedIn,
    bool Cancelled);
