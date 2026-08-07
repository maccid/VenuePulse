namespace VenuePulse.AccessControl.Api.Hubs;

using Microsoft.AspNetCore.SignalR;
using VenuePulse.AccessControl.Application;

public sealed class SignalRZoneOccupancyNotifier(IHubContext<OccupancyHub> hubContext) : IZoneOccupancyNotifier
{
    public Task NotifyOccupancyChangedAsync(ZoneOccupancyDto occupancy, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("occupancyChanged", occupancy, cancellationToken);
}
