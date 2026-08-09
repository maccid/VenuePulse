namespace VenuePulse.Ticketing.Api.Hubs;

using Microsoft.AspNetCore.SignalR;
using VenuePulse.Ticketing.Application;

public sealed class SignalRPricingNotifier(IHubContext<PricingHub> hubContext) : IPricingNotifier
{
    public Task NotifyPricingChangedAsync(
        Guid eventId, IReadOnlyCollection<TicketTypeDto> ticketTypes, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("pricingChanged", new { eventId, ticketTypes }, cancellationToken);
}
