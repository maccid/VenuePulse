namespace VenuePulse.CheckIn.Api.Hubs;

using Microsoft.AspNetCore.SignalR;

/// <summary>
/// Scoping por evento vía grupos de SignalR (ADR 0029): cada cliente entra al grupo
/// "event-{eventId}" con <see cref="JoinEvent"/> y solo recibe el estado de sus tickets.
/// </summary>
public sealed class TicketStatusHub : Hub
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
