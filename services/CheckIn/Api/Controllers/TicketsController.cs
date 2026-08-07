namespace VenuePulse.CheckIn.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using VenuePulse.CheckIn.Application;
using VenuePulse.CheckIn.Application.Commands;
using VenuePulse.CheckIn.Application.Queries;

public sealed record PurchaseTicketRequest(Guid EventId, Guid TicketTypeId, Guid ZoneId, string Holder, bool SimulateFailure);

[ApiController]
[Route("api/tickets")]
public sealed class TicketsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<object>> Purchase(PurchaseTicketRequest request, CancellationToken cancellationToken)
    {
        var ticketId = await sender.Send(
            new PurchaseTicketCommand(request.EventId, request.TicketTypeId, request.ZoneId, request.Holder, request.SimulateFailure),
            cancellationToken);

        return Accepted(new { ticketId });
    }

    [HttpGet("{ticketId:guid}/status")]
    public async Task<ActionResult<TicketStatusDto>> GetStatus(Guid ticketId, CancellationToken cancellationToken)
    {
        var status = await sender.Send(new GetTicketStatusQuery(ticketId), cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TicketSummaryDto>>> GetMyTickets(
        [FromQuery] string holder, CancellationToken cancellationToken)
    {
        var tickets = await sender.Send(new GetMyTicketsQuery(holder), cancellationToken);
        return Ok(tickets);
    }

    [HttpPost("{ticketId:guid}/check-in")]
    public async Task<IActionResult> CheckIn(Guid ticketId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CheckInCommand(ticketId), cancellationToken);
        return result.IsSuccess ? Ok() : BadRequest(new { error = result.Error });
    }
}
