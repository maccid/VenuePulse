using MediatR;

namespace VenuePulse.Core.Domain;

/// <summary>
/// Evento interno al agregado que lo genera. Se despacha in-process (MediatR) y, si el
/// handler correspondiente decide que también interesa fuera del servicio, se traduce a un
/// <see cref="Messaging.IntegrationEvent"/> publicado vía el Outbox — nunca se serializa
/// este tipo directamente al bus.
/// </summary>
public interface IDomainEvent : INotification
{
    DateTimeOffset OccurredOnUtc { get; }
}
