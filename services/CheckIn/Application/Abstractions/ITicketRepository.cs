namespace VenuePulse.CheckIn.Application;

using VenuePulse.CheckIn.Domain;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Ticket>> GetByHolderAsync(string holder, CancellationToken cancellationToken);

    Task<IReadOnlyList<Ticket>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Ticket>> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken);

    Task AddAsync(Ticket ticket, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
