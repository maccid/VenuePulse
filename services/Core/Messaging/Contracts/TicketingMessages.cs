namespace VenuePulse.Core.Messaging.Contracts;

using VenuePulse.Core.Domain;

// Evento que Ticketing publica al sembrar/crear un Event, consumido por AccessControl para
// aprovisionar sus zonas fijas ya vinculadas a ese evento (ver .ia/KNOWN-ISSUES.md).
// "Id" y no "EventId": IntegrationEvent.EventId ya es el Id del mensaje, no del Event de dominio.
// "Type" decide qué set de zonas aprovisiona AccessControl (partido: gradas/puertas; concierto:
// pista + gradas) — ver ZoneService.ZonesFor en AccessControl.Application.
public sealed record EventCreated(Guid Id, string Name, string VenueName, DateTimeOffset StartsAtUtc, EventType Type) : IntegrationEvent;
