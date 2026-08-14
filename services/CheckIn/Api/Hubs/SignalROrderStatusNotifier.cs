namespace VenuePulse.CheckIn.Api.Hubs;

using Microsoft.AspNetCore.SignalR;
using VenuePulse.CheckIn.Application;

/// <summary>Reutiliza el hub <c>/hubs/tickets</c> ya existente con un evento nuevo
/// (<c>orderStatusChanged</c>) — sin hub propio, ver ADR 0028 punto 6.</summary>
public sealed class SignalROrderStatusNotifier(IHubContext<TicketStatusHub> hubContext) : IOrderStatusNotifier
{
    public Task NotifyStatusChangedAsync(OrderStatusDto status, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("orderStatusChanged", status, cancellationToken);
}
