namespace VenuePulse.CheckIn.Infrastructure.Sagas.Timeout;

using System.Collections.Concurrent;
using VenuePulse.CheckIn.Application;

/// <summary>
/// Registro en memoria de reservas en curso y su deadline — ver ADR 0010 sobre por qué no se usa
/// un <c>IMessageScheduler</c> de MassTransit (Quartz o RabbitMQ delayed-exchange añadirían
/// infraestructura sin necesidad real para una demo). No sobrevive a un reinicio del servicio,
/// igual que el resto de mitigaciones de arranque en frío documentadas en KNOWN-ISSUES.md.
/// Implementa <see cref="IReservationTimeoutReader"/> para que la Application pueda leer el
/// deadline (contador de reserva, ADR 0025) sin depender de esta capa.
/// </summary>
public sealed class ReservationTimeoutTracker : IReservationTimeoutReader
{
    private readonly ConcurrentDictionary<Guid, (Guid ZoneId, DateTimeOffset DeadlineUtc)> _pending = new();

    public void Track(Guid correlationId, Guid zoneId, DateTimeOffset deadlineUtc) =>
        _pending[correlationId] = (zoneId, deadlineUtc);

    public void Untrack(Guid correlationId) => _pending.TryRemove(correlationId, out _);

    // Expone el deadline vigente de una reserva para que la Api/notificaciones en tiempo real
    // puedan mostrar una cuenta atrás — misma fuente de verdad que usa este propio tracker para
    // expirar reservas (ver ADR 0025), sin duplicar el cálculo "ahora + timeout" en otro sitio.
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
