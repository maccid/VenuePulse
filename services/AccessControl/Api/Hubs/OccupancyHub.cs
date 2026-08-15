namespace VenuePulse.AccessControl.Api.Hubs;

using Microsoft.AspNetCore.SignalR;

/// <summary>Notifica en vivo los cambios de aforo por evento — cada cliente entra al grupo del
/// evento con <see cref="JoinEvent"/> y recibe sus actualizaciones.</summary>
public sealed class OccupancyHub : Hub
{
    public async Task JoinEvent(Guid eventId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(eventId));
    }

    public async Task LeaveEvent(Guid eventId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(eventId));
    }

    internal static string GroupName(Guid eventId) => $"event-{eventId}";
}
