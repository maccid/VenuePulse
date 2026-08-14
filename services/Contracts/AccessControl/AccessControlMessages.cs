namespace VenuePulse.Core.Messaging.Contracts;

// Comandos que la Saga de CheckIn (TicketPurchaseSaga) envía a AccessControl.
// Nota de simplicidad (ver ADR 0004): un sistema más grande separaría comandos y eventos con
// interfaces marcador distintas; aquí comparten IntegrationEvent para no añadir ceremonia.
public sealed record ReserveZoneSlot(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record ConfirmZoneSlot(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record ReleaseZoneSlot(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

// Compensación por cancelación del evento (Bloque 16-B, ADR 0020): lo publica CheckIn ticket a
// ticket al reembolsar una entrada YA confirmada. Es simétrico pero distinto de ReleaseZoneSlot:
// esto decrementa ConfirmedSold, no Reserved (una reserva a medio hacer la libera ReleaseZoneSlot).
public sealed record ReleaseConfirmedZoneSlot(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record RecordZoneCheckIn(Guid CorrelationId, Guid ZoneId, Guid TicketId) : IntegrationEvent;

// Eventos que AccessControl publica de vuelta a la Saga.
public sealed record ZoneSlotReserved(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record ZoneSlotReservationFailed(Guid CorrelationId, Guid ZoneId, string Reason) : IntegrationEvent;

public sealed record ZoneSlotConfirmed(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record ZoneSlotReleased(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record ZoneCheckedIn(Guid CorrelationId, Guid ZoneId, Guid TicketId, int CurrentOccupancy) : IntegrationEvent;

// Señal de demanda para Ticketing (precio dinámico, Bloque 9): se publica cada vez que cambia el
// aforo de una zona (venta, reserva, check-in o motor de simulación), reutilizando el mismo punto
// de notificación que ya usa AccessControl para el SignalR en vivo. Es un snapshot que se
// sobrescribe con el último valor conocido, no un contador que se incrementa — por eso no necesita
// el Inbox real que sí protege a los comandos de zona (ver ADR 0015): procesar el mismo mensaje
// dos veces no cambia el resultado.
// "Id" y no "EventId": IntegrationEvent.EventId ya es el Id del mensaje, no del Event de dominio
// al que pertenece la zona (mismo motivo que EventCreated).
public sealed record ZoneOccupancyChanged(
    Guid ZoneId, Guid Id, int Capacity, int AvailableToSell, int ConfirmedSold, int CheckedIn) : IntegrationEvent;
