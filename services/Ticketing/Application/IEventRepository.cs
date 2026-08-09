namespace VenuePulse.Ticketing.Application;

using VenuePulse.Ticketing.Domain;

public interface IEventRepository
{
    Task<IReadOnlyList<Event>> GetAllAsync(CancellationToken cancellationToken);

    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Event @event, CancellationToken cancellationToken);
}
