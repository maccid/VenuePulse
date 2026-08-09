namespace VenuePulse.Core.Messaging.Contracts;

// Evento que Ticketing publica al sembrar/crear un Event, consumido por AccessControl para
// aprovisionar sus zonas fijas ya vinculadas a ese evento (ver .ia/KNOWN-ISSUES.md).
// "Id" y no "EventId": IntegrationEvent.EventId ya es el Id del mensaje, no del Event de dominio.
public sealed record EventCreated(Guid Id, string Name, string VenueName, DateTimeOffset StartsAtUtc) : IntegrationEvent;
