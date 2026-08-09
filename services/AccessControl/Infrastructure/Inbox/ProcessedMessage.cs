namespace VenuePulse.AccessControl.Infrastructure.Inbox;

/// <summary>
/// Marca de que un mensaje de integración ya se procesó, keyed por <c>IntegrationEvent.EventId</c>
/// (identidad del mensaje en el bus, no un id de negocio) — ver ADR 0015. Vive en Infrastructure,
/// no en el dominio: es un detalle técnico de entrega "como mucho una vez" sobre un transporte
/// "al menos una vez", sin ningún invariante de negocio.
/// </summary>
public sealed class ProcessedMessage
{
    public Guid EventId { get; private set; }

    public string MessageType { get; private set; } = string.Empty;

    public DateTimeOffset ProcessedAtUtc { get; private set; }

    private ProcessedMessage()
    {
        // EF Core
    }

    public ProcessedMessage(Guid eventId, string messageType, DateTimeOffset processedAtUtc)
    {
        EventId = eventId;
        MessageType = messageType;
        ProcessedAtUtc = processedAtUtc;
    }
}
