namespace VenuePulse.CheckIn.Api.Hubs;

using Microsoft.AspNetCore.SignalR;

/// <summary>Hub de estado de entradas y pedidos: agrupa a los clientes por evento para notificarles cambios de estado.</summary>
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
