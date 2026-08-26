namespace VenuePulse.Core.Messaging.Contracts;

public sealed record ReserveZoneSlot(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record ConfirmZoneSlot(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record ReleaseZoneSlot(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record ReleaseConfirmedZoneSlot(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record RecordZoneCheckIn(Guid CorrelationId, Guid ZoneId, Guid TicketId) : IntegrationEvent;

public sealed record ZoneSlotReserved(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record ZoneSlotReservationFailed(Guid CorrelationId, Guid ZoneId, string Reason) : IntegrationEvent;

public sealed record ZoneSlotConfirmed(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record ZoneSlotReleased(Guid CorrelationId, Guid ZoneId) : IntegrationEvent;

public sealed record ZoneCheckedIn(Guid CorrelationId, Guid ZoneId, Guid TicketId, int CurrentOccupancy) : IntegrationEvent;

public sealed record ZoneOccupancyChanged(
    Guid ZoneId, Guid Id, string Name, int Capacity, int AvailableToSell, int ConfirmedSold, int CheckedIn) : IntegrationEvent;

public sealed record SimulationReset(Guid Id) : IntegrationEvent;
