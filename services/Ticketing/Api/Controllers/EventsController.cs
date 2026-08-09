namespace VenuePulse.Ticketing.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using VenuePulse.Ticketing.Application;

[ApiController]
[Route("api/events")]
public sealed class EventsController(ITicketingService ticketingService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<EventSummaryDto>>> GetEvents(CancellationToken cancellationToken)
    {
        var events = await ticketingService.GetEventsAsync(cancellationToken);
        return Ok(events);
    }

    [HttpGet("{eventId:guid}/ticket-types")]
    public async Task<ActionResult<IReadOnlyCollection<TicketTypeDto>>> GetTicketTypes(
        Guid eventId, CancellationToken cancellationToken)
    {
        var ticketTypes = await ticketingService.GetTicketTypesAsync(eventId, cancellationToken);
        return ticketTypes is null ? NotFound() : Ok(ticketTypes);
    }
}
