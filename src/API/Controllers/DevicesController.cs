using Application.Devices.Commands;
using Application.Devices.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("devices")]
public sealed class DevicesController : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterDeviceRequest request,
        [FromServices] RegisterDeviceCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new RegisterDeviceCommand(request.DeviceCode, request.MaxTemperatureThreshold);
        var deviceId = await handler.Handle(command, cancellationToken);
        return Created($"/devices/{deviceId}/status", new RegisterDeviceResponse(deviceId));
    }

    [HttpPost("{deviceId:guid}/start")]
    public async Task<IActionResult> Start(
        Guid deviceId,
        [FromBody] StartDeviceRequest request,
        [FromServices] StartDeviceCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new StartDeviceCommand(deviceId, request.CurrentTemperature);
        await handler.Handle(command, cancellationToken);
        return Accepted($"/devices/{deviceId}/status");
    }

    [HttpGet("{deviceId:guid}/status")]
    public async Task<IActionResult> GetStatus(
        Guid deviceId,
        [FromServices] GetDeviceStatusQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetDeviceStatusQuery(deviceId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}

public sealed record RegisterDeviceRequest(string DeviceCode, double MaxTemperatureThreshold);

public sealed record RegisterDeviceResponse(Guid DeviceId);

public sealed record StartDeviceRequest(double CurrentTemperature);
