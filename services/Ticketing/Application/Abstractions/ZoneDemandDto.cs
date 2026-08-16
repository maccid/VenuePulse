namespace VenuePulse.Ticketing.Application;

public sealed record ZoneDemandDto(Guid ZoneId, string Name, int Capacity, int AvailableToSell);
