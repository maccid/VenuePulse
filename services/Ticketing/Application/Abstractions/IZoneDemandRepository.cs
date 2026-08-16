namespace VenuePulse.Ticketing.Application;

public interface IZoneDemandRepository
{
    Task UpsertAsync(
        Guid zoneId,
        Guid eventId,
        string name,
        int capacity,
        int availableToSell,
        int confirmedSold,
        int checkedIn,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ZoneDemandDto>> GetAllAsync(Guid eventId, CancellationToken cancellationToken);
}
