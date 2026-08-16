namespace VenuePulse.AccessControl.Api.Hubs;

using MassTransit;
using Microsoft.AspNetCore.SignalR;
using VenuePulse.AccessControl.Application;
using VenuePulse.Core.Messaging.Contracts;

public sealed class SignalRZoneOccupancyNotifier(IHubContext<OccupancyHub> hubContext, IPublishEndpoint publishEndpoint)
    : IZoneOccupancyNotifier
{
    public async Task NotifyOccupancyChangedAsync(ZoneOccupancyDto occupancy, CancellationToken cancellationToken)
    {
        await hubContext.Clients.Group(OccupancyHub.GroupName(occupancy.EventId)).SendAsync("occupancyChanged", occupancy, cancellationToken);

        await publishEndpoint.Publish(
            new ZoneOccupancyChanged(
                occupancy.ZoneId,
                Id: occupancy.EventId,
                occupancy.Name,
                occupancy.Capacity,
                occupancy.AvailableToSell,
                occupancy.ConfirmedSold,
                occupancy.CheckedIn),
            cancellationToken);
    }
}
