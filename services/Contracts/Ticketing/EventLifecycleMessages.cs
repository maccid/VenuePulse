namespace VenuePulse.Core.Messaging.Contracts;

public sealed record EventCancelled(Guid Id) : IntegrationEvent;
