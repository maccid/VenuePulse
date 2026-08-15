namespace VenuePulse.AccessControl.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using VenuePulse.AccessControl.Application;
using VenuePulse.AccessControl.Application.Prediction;

[ApiController]
[Route("api/prediction")]
public sealed class PredictionController(IOccupancyPredictionService predictionService) : ControllerBase
{
    /// <summary>Predice la curva de llegada y los no-shows de un evento sobre la serie temporal
    /// del simulador. 404 si el evento aún no tiene snapshots.</summary>
    [HttpGet("events/{eventId:guid}")]
    public async Task<ActionResult<OccupancyPredictionDto>> Get(
        Guid eventId,
        [FromQuery] DateTimeOffset? eventStartsAtUtc,
        CancellationToken cancellationToken)
    {
        if (eventStartsAtUtc is null)
            return BadRequest("eventStartsAtUtc es obligatorio para definir la ventana de llegada.");

        var prediction = await predictionService.PredictAsync(eventId, eventStartsAtUtc.Value, cancellationToken);
        if (prediction is null)
            return NotFound();

        return Ok(prediction);
    }
}
