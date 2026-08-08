namespace VenuePulse.CheckIn.Infrastructure.Sagas;

using System.Collections.Concurrent;

/// <summary>
/// Registro en memoria de reservas en curso y su deadline — ver ADR 0010 sobre por qué no se usa
/// un <c>IMessageScheduler</c> de MassTransit (Quartz o RabbitMQ delayed-exchange añadirían
/// infraestructura sin necesidad real para una demo). No sobrevive a un reinicio del servicio,
/// igual que el resto de mitigaciones de arranque en frío documentadas en KNOWN-ISSUES.md.
/// </summary>
public sealed class ReservationTimeoutTracker
{
    private readonly ConcurrentDictionary<Guid, (Guid ZoneId, DateTimeOffset DeadlineUtc)> _pending = new();

    public void Track(Guid correlationId, Guid zoneId, DateTimeOffset deadlineUtc) =>
        _pending[correlationId] = (zoneId, deadlineUtc);

    public void Untrack(Guid correlationId) => _pending.TryRemove(correlationId, out _);

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
