using Application.Devices.Projections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("projections")]
public sealed class ProjectionsController : ControllerBase
{
    [HttpPost("devices/rebuild")]
    public async Task<IActionResult> RebuildDeviceProjection(
        [FromServices] RebuildDeviceReadModelHandler handler,
        CancellationToken cancellationToken)
    {
        var projectedEvents = await handler.Handle(cancellationToken);
        return Ok(new RebuildProjectionResponse(projectedEvents));
    }
}

public sealed record RebuildProjectionResponse(int ProjectedEvents);
