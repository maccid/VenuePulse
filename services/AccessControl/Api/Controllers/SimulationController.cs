namespace VenuePulse.AccessControl.Api.Controllers;

using MassTransit;
using Microsoft.AspNetCore.Mvc;
using VenuePulse.AccessControl.Application;
using VenuePulse.AccessControl.Application.Simulation;
using VenuePulse.Core.Messaging.Contracts;

[ApiController]
[Route("api/simulation")]
public sealed class SimulationController(ISimulationEngine engine, IZoneService zoneService, IPublishEndpoint publishEndpoint) : ControllerBase
{
    /// <summary>Arranca un run de simulación para un evento. 404 si el evento no tiene zonas;
    /// 409 si ya hay un run en curso.</summary>
    [HttpPost("events/{eventId:guid}/start")]
    public async Task<ActionResult<SimulationRunStatusDto>> Start(
        Guid eventId,
        [FromBody] SimulationStartRequest request,
        CancellationToken cancellationToken)
    {
        var zones = await zoneService.GetAllOccupancyAsync(eventId, cancellationToken);
        if (zones.Count == 0)
            return NotFound();

        if (!await engine.TryStartAsync(eventId, request, cancellationToken))
            return Conflict($"Ya hay una simulación en curso para el evento {eventId}.");

        return Accepted(engine.GetStatus(eventId));
    }

    [HttpPost("events/{eventId:guid}/stop")]
    public IActionResult Stop(Guid eventId)
    {
        if (!engine.Stop(eventId))
            return NotFound();

        return NoContent();
    }

    [HttpGet("events/{eventId:guid}/status")]
    public ActionResult<SimulationRunStatusDto?> Status(Guid eventId) => Ok(engine.GetStatus(eventId));

    /// <summary>Reinicia el demo: para el run, pone los contadores a cero y notifica a CheckIn para cancelar entradas.</summary>
    [HttpPost("events/{eventId:guid}/reset")]
    public async Task<IActionResult> Reset(Guid eventId, CancellationToken cancellationToken)
    {
        engine.Stop(eventId);
        await zoneService.ResetSimulationAsync(eventId, cancellationToken);
        await publishEndpoint.Publish(new SimulationReset(eventId), cancellationToken);
        return NoContent();
    }

    [HttpPost("events/{eventId:guid}/reset-arrivals")]
    public async Task<IActionResult> ResetArrivals(Guid eventId, CancellationToken cancellationToken)
    {
        engine.Stop(eventId);
        await zoneService.ResetArrivalsSimulationAsync(eventId, cancellationToken);
        return NoContent();
    }
}
