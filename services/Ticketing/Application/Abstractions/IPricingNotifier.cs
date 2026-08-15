namespace VenuePulse.Ticketing.Application;

public interface IPricingNotifier
{
    Task NotifyPricingChangedAsync(Guid eventId, IReadOnlyCollection<TicketTypeDto> ticketTypes, CancellationToken cancellationToken);
}
