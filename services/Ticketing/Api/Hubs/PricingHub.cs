namespace VenuePulse.Ticketing.Api.Hubs;

using Microsoft.AspNetCore.SignalR;

/// <summary>Hub de SignalR para notificar cambios de precio en vivo, agrupando clientes por evento.</summary>
public sealed class PricingHub : Hub
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
