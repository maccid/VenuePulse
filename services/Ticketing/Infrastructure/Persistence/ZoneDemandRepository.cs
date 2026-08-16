namespace VenuePulse.Ticketing.Infrastructure;

using Microsoft.EntityFrameworkCore;
using VenuePulse.Ticketing.Application;

public sealed class ZoneDemandRepository(TicketingDbContext dbContext) : IZoneDemandRepository
{
    public async Task UpsertAsync(
        Guid zoneId,
        Guid eventId,
        string name,
        int capacity,
        int availableToSell,
        int confirmedSold,
        int checkedIn,
        CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.ZoneDemandSnapshots.FindAsync([zoneId], cancellationToken);

        if (snapshot is null)
        {
            dbContext.ZoneDemandSnapshots.Add(new ZoneDemandSnapshot
            {
                ZoneId = zoneId,
                EventId = eventId,
                Name = name,
                Capacity = capacity,
                AvailableToSell = availableToSell,
                ConfirmedSold = confirmedSold,
                CheckedIn = checkedIn,
            });
        }
        else
        {
            snapshot.EventId = eventId;
            snapshot.Name = name;
            snapshot.Capacity = capacity;
            snapshot.AvailableToSell = availableToSell;
            snapshot.ConfirmedSold = confirmedSold;
            snapshot.CheckedIn = checkedIn;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ZoneDemandDto>> GetAllAsync(Guid eventId, CancellationToken cancellationToken) =>
        await dbContext.ZoneDemandSnapshots
            .AsNoTracking()
            .Where(s => s.EventId == eventId)
            .Select(s => new ZoneDemandDto(s.ZoneId, s.Name, s.Capacity, s.AvailableToSell))
            .ToListAsync(cancellationToken);
}
