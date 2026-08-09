namespace VenuePulse.Ticketing.Application;

/// <summary>
/// Puerto hacia el exterior en tiempo real (SignalR) cuando el precio efectivo de los tipos de
/// entrada de un evento cambia — implementación concreta en la Api, mismo patrón que
/// IZoneOccupancyNotifier en AccessControl.
/// </summary>
public interface IPricingNotifier
{
    Task NotifyPricingChangedAsync(Guid eventId, IReadOnlyCollection<TicketTypeDto> ticketTypes, CancellationToken cancellationToken);
}
