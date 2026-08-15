namespace VenuePulse.AccessControl.Infrastructure.Inbox;

public sealed class ProcessedMessage
{
    public Guid EventId { get; private set; }

    public string MessageType { get; private set; } = string.Empty;

    public DateTimeOffset ProcessedAtUtc { get; private set; }

    private ProcessedMessage()
    {
    }

    public ProcessedMessage(Guid eventId, string messageType, DateTimeOffset processedAtUtc)
    {
        EventId = eventId;
        MessageType = messageType;
        ProcessedAtUtc = processedAtUtc;
    }
}
