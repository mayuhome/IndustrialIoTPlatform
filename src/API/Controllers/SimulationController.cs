using Application.Simulation.Commands;
using Application.Simulation.Abstractions;
using Application.Simulation.Models;
using Application.Simulation.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("simulation/devices")]
public sealed class SimulationController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromServices] GetAllSimulatedDevicesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetAllSimulatedDevicesQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(
        [FromBody] GenerateSimulatedDevicesRequest request,
        [FromServices] GenerateSimulatedDevicesCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new GenerateSimulatedDevicesCommand(request.DeviceCount, request.DeviceCodePrefix),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(
        [FromServices] ResetSimulatedDevicesCommandHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.Handle(new ResetSimulatedDevicesCommand(), cancellationToken);
        return NoContent();
    }

    [HttpPost("advance")]
    public async Task<IActionResult> Advance(
        [FromServices] ISimulatedDeviceSimulationService service,
        CancellationToken cancellationToken)
    {
        var updated = await service.AdvanceAsync(cancellationToken);
        return Ok(new AdvanceSimulatedDevicesResponse(updated));
    }
}

public sealed record GenerateSimulatedDevicesRequest(int DeviceCount, string? DeviceCodePrefix);

public sealed record AdvanceSimulatedDevicesResponse(int UpdatedCount);
