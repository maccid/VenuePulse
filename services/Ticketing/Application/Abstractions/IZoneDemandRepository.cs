namespace VenuePulse.Ticketing.Application;

public interface IZoneDemandRepository
{
    Task UpsertAsync(
        Guid zoneId,
        Guid eventId,
        int capacity,
        int availableToSell,
        int confirmedSold,
        int checkedIn,
        CancellationToken cancellationToken);

    Task<decimal?> GetOccupancyPercentageAsync(Guid eventId, CancellationToken cancellationToken);
}
