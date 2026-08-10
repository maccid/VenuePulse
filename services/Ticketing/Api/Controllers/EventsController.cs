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

    /// <summary>Alta de evento desde Operaciones (venue-ops) — hasta ahora los eventos solo
    /// nacían de seed data (ver ADR 0019).</summary>
    [HttpPost]
    public async Task<ActionResult<EventSummaryDto>> CreateEvent(CreateEventRequest request, CancellationToken cancellationToken)
    {
        var created = await ticketingService.CreateEventAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetTicketTypes), new { eventId = created.Id }, created);
    }

    [HttpPost("{eventId:guid}/close-sales")]
    public async Task<IActionResult> CloseSales(Guid eventId, CancellationToken cancellationToken)
    {
        var result = await ticketingService.CloseSalesAsync(eventId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{eventId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid eventId, CancellationToken cancellationToken)
    {
        var result = await ticketingService.CancelEventAsync(eventId, cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(EventLifecycleResult result) => result switch
    {
        EventLifecycleResult.Success => NoContent(),
        EventLifecycleResult.NotFound => NotFound(),
        EventLifecycleResult.InvalidState => Conflict(),
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
    };
}
