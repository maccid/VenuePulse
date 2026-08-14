namespace VenuePulse.CheckIn.Api.Hubs;

using Microsoft.AspNetCore.SignalR;
using VenuePulse.CheckIn.Application;

public sealed class SignalRTicketStatusNotifier(IHubContext<TicketStatusHub> hubContext) : ITicketStatusNotifier
{
    public Task NotifyStatusChangedAsync(TicketStatusDto status, CancellationToken cancellationToken) =>
        hubContext.Clients.Group(TicketStatusHub.GroupName(status.EventId)).SendAsync("ticketStatusChanged", status, cancellationToken);
}
