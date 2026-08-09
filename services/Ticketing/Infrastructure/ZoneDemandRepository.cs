namespace VenuePulse.Ticketing.Infrastructure;

using Microsoft.EntityFrameworkCore;
using VenuePulse.Ticketing.Application;
using VenuePulse.Ticketing.Domain;

public sealed class ZoneDemandRepository(TicketingDbContext dbContext) : IZoneDemandRepository
{
    public async Task UpsertAsync(
        Guid zoneId,
        Guid eventId,
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
                Capacity = capacity,
                AvailableToSell = availableToSell,
                ConfirmedSold = confirmedSold,
                CheckedIn = checkedIn,
            });
        }
        else
        {
            snapshot.EventId = eventId;
            snapshot.Capacity = capacity;
            snapshot.AvailableToSell = availableToSell;
            snapshot.ConfirmedSold = confirmedSold;
            snapshot.CheckedIn = checkedIn;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<decimal?> GetOccupancyPercentageAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var totals = await dbContext.ZoneDemandSnapshots
            .AsNoTracking()
            .Where(s => s.EventId == eventId)
            .GroupBy(_ => 1)
            .Select(g => new { TotalCapacity = g.Sum(s => s.Capacity), TotalAvailableToSell = g.Sum(s => s.AvailableToSell) })
            .FirstOrDefaultAsync(cancellationToken);

        return totals is null
            ? null
            : DynamicPricing.CalculateOccupancyPercentage(totals.TotalCapacity, totals.TotalAvailableToSell);
    }
}
