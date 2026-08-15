namespace VenuePulse.AccessControl.Application;

public interface IZoneOccupancyNotifier
{
    Task NotifyOccupancyChangedAsync(ZoneOccupancyDto occupancy, CancellationToken cancellationToken);
}
