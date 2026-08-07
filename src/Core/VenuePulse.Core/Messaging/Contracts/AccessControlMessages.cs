namespace VenuePulse.Core.Messaging.Contracts;

// Comandos que la Saga de CheckIn (TicketPurchaseSaga) envía a AccessControl.
// Nota de simplicidad (ver ADR 0004): un sistema más grande separaría comandos y eventos con
// interfaces marcador distintas; aquí comparten IntegrationEvent para no añadir ceremonia.
// SimulateFailure viaja en el propio mensaje (no se relee del estado persistido de la Saga)
// para que la decisión de la Saga dependa únicamente del evento recién recibido, no de una
// relectura de Mongo — más simple de razonar y evita un salto de red/IO innecesario.
public sealed record ReserveZoneSlot(Guid CorrelationId, Guid ZoneId, bool SimulateFailure) : IntegrationEvent;

public sealed record ConfirmZoneSlot(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record ReleaseZoneSlot(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record RecordZoneCheckIn(Guid CorrelationId, Guid ZoneId, Guid TicketId) : IntegrationEvent;

// Eventos que AccessControl publica de vuelta a la Saga.
public sealed record ZoneSlotReserved(Guid CorrelationId, Guid ZoneId, bool SimulateFailure) : IntegrationEvent;

public sealed record ZoneSlotReservationFailed(Guid CorrelationId, Guid ZoneId, string Reason) : IntegrationEvent;

public sealed record ZoneSlotConfirmed(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record ZoneSlotReleased(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record ZoneCheckedIn(Guid CorrelationId, Guid ZoneId, Guid TicketId, int CurrentOccupancy) : IntegrationEvent;
