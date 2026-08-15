namespace VenuePulse.CheckIn.Api.Hubs;

using Microsoft.AspNetCore.SignalR;
using VenuePulse.CheckIn.Application;

public sealed class SignalROrderStatusNotifier(IHubContext<TicketStatusHub> hubContext) : IOrderStatusNotifier
{
    public Task NotifyStatusChangedAsync(OrderStatusDto status, CancellationToken cancellationToken) =>
        hubContext.Clients.Group(TicketStatusHub.GroupName(status.EventId)).SendAsync("orderStatusChanged", status, cancellationToken);
}
