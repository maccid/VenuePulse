namespace VenuePulse.Core.Messaging.Contracts;

// Evento que Ticketing publica al cancelar un Event (solo posible mientras está OnSale, ver
// Event.Cancel() y ADR 0019) — fichero nuevo, separado de TicketingMessages.cs, para minimizar
// conflictos de merge con otras ramas en paralelo (ver .ia/TODO.md, ronda 3).
// Consumido por AccessControl (cierra/bloquea las zonas del evento, ADR 0020) y por CheckIn
// (reembolsa/rechaza las entradas del evento, ADR 0021).
public sealed record EventCancelled(Guid Id) : IntegrationEvent;
