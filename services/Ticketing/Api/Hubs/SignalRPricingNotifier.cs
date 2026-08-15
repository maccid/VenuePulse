namespace VenuePulse.Ticketing.Api.Hubs;

using Microsoft.AspNetCore.SignalR;
using VenuePulse.Ticketing.Application;

/// <summary>Publica el cambio de precios de un evento a su grupo de SignalR.</summary>
public sealed class SignalRPricingNotifier(IHubContext<PricingHub> hubContext) : IPricingNotifier
{
    public Task NotifyPricingChangedAsync(
        Guid eventId, IReadOnlyCollection<TicketTypeDto> ticketTypes, CancellationToken cancellationToken) =>
        hubContext.Clients.Group(PricingHub.GroupName(eventId)).SendAsync("pricingChanged", new { eventId, ticketTypes }, cancellationToken);
}
