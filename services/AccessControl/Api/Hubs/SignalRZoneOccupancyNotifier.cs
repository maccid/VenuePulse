namespace VenuePulse.AccessControl.Api.Hubs;

using MassTransit;
using Microsoft.AspNetCore.SignalR;
using VenuePulse.AccessControl.Application;
using VenuePulse.Core.Messaging.Contracts;

/// <summary>
/// Además del push en vivo al navegador (SignalR), publica <see cref="ZoneOccupancyChanged"/>
/// hacia el bus: es la misma noticia ("cambió el aforo de esta zona") con dos audiencias
/// distintas, así que reutiliza el único punto de notificación que ya llama <c>ZoneService</c> en
/// cada operación, en vez de repartir el publish entre los 4 consumers + el controller de
/// simulación que mutan una zona.
/// </summary>
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
                occupancy.Capacity,
                occupancy.AvailableToSell,
                occupancy.ConfirmedSold,
                occupancy.CheckedIn),
            cancellationToken);
    }
}
