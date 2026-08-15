namespace VenuePulse.CheckIn.Infrastructure.Sagas.Timeout;

using System.Collections.Concurrent;
using VenuePulse.CheckIn.Application;

public sealed class ReservationTimeoutTracker : IReservationTimeoutReader
{
    private readonly ConcurrentDictionary<Guid, (Guid ZoneId, DateTimeOffset DeadlineUtc)> _pending = new();

    public void Track(Guid correlationId, Guid zoneId, DateTimeOffset deadlineUtc) =>
        _pending[correlationId] = (zoneId, deadlineUtc);

    public void Untrack(Guid correlationId) => _pending.TryRemove(correlationId, out _);

    public bool TryGetDeadline(Guid correlationId, out DateTimeOffset deadlineUtc)
    {
        if (_pending.TryGetValue(correlationId, out var entry))
        {
            deadlineUtc = entry.DeadlineUtc;
            return true;
        }

        deadlineUtc = default;
        return false;
    }

    public IReadOnlyCollection<(Guid CorrelationId, Guid ZoneId)> SweepExpired(DateTimeOffset nowUtc)
    {
        var expired = _pending
            .Where(kvp => kvp.Value.DeadlineUtc <= nowUtc)
            .Select(kvp => (kvp.Key, kvp.Value.ZoneId))
            .ToList();

        foreach (var (correlationId, _) in expired)
            _pending.TryRemove(correlationId, out _);

        return expired;
    }
}
