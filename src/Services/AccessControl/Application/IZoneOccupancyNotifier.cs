namespace VenuePulse.AccessControl.Application;

/// <summary>
/// Puerto hacia el exterior en tiempo real (SignalR). La implementación concreta vive en la Api
/// (es infraestructura de framework web), la Application solo conoce esta abstracción.
/// </summary>
public interface IZoneOccupancyNotifier
{
    Task NotifyOccupancyChangedAsync(ZoneOccupancyDto occupancy, CancellationToken cancellationToken);
}
