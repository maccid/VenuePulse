namespace VenuePulse.AccessControl.Application;

public sealed record ZoneOccupancyDto(
    Guid ZoneId,
    string Name,
    int Capacity,
    int ConfirmedSold,
    int CheckedIn);
