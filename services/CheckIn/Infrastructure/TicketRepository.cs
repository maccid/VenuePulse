namespace VenuePulse.CheckIn.Infrastructure;

using Microsoft.EntityFrameworkCore;
using VenuePulse.CheckIn.Application;
using VenuePulse.CheckIn.Domain;

public sealed class TicketRepository(CheckInDbContext dbContext) : ITicketRepository
{
    public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Ticket>> GetByHolderAsync(string holder, CancellationToken cancellationToken) =>
        await dbContext.Tickets
            .AsNoTracking()
            .Where(t => t.Holder == holder)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Ticket>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken) =>
        await dbContext.Tickets
            .AsNoTracking()
            .Where(t => t.EventId == eventId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Ticket ticket, CancellationToken cancellationToken) =>
        await dbContext.Tickets.AddAsync(ticket, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
