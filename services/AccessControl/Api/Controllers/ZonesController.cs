namespace VenuePulse.AccessControl.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using VenuePulse.AccessControl.Application;

[ApiController]
[Route("api/zones")]
public sealed class ZonesController(IZoneService zoneService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ZoneOccupancyDto>>> GetAll(CancellationToken cancellationToken)
    {
        var zones = await zoneService.GetAllOccupancyAsync(cancellationToken);
        return Ok(zones);
    }

    [HttpGet("{zoneId:guid}/occupancy")]
    public async Task<ActionResult<ZoneOccupancyDto>> GetOccupancy(Guid zoneId, CancellationToken cancellationToken)
    {
        var occupancy = await zoneService.GetOccupancyAsync(zoneId, cancellationToken);
        return occupancy is null ? NotFound() : Ok(occupancy);
    }
}
