namespace VenuePulse.Ticketing.Infrastructure;

using Microsoft.EntityFrameworkCore;
using VenuePulse.Ticketing.Application;
using VenuePulse.Ticketing.Domain;

public sealed class EventRepository(TicketingDbContext dbContext) : IEventRepository
{
    public async Task<IReadOnlyList<Event>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Events
            .AsNoTracking()
            .OrderBy(e => e.StartsAtUtc)
            .ToListAsync(cancellationToken);

    public Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Events
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task AddAsync(Event @event, CancellationToken cancellationToken) =>
        await dbContext.Events.AddAsync(@event, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
