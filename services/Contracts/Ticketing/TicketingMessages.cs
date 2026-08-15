namespace VenuePulse.Core.Messaging.Contracts;

using VenuePulse.Core.Domain;

public sealed record EventCreated(Guid Id, string Name, string VenueName, DateTimeOffset StartsAtUtc, EventType Type) : IntegrationEvent;
