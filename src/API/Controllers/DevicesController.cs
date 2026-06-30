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
        var trace = CommandTraceHeaders.Read(Request);
        var command = new RegisterDeviceCommand(
            request.DeviceCode,
            request.MaxTemperatureThreshold,
            trace.CorrelationId,
            trace.CausationId);
        var deviceId = await handler.Handle(command, cancellationToken);
        return Created($"/devices/{deviceId}/status", new RegisterDeviceResponse(deviceId));
    }

    // get all devices for current user
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromServices] GetAllDevicesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetAllDevicesQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{deviceId:guid}/start")]
    public async Task<IActionResult> Start(
        Guid deviceId,
        [FromBody] StartDeviceRequest request,
        [FromServices] StartDeviceCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var trace = CommandTraceHeaders.Read(Request);
        var command = new StartDeviceCommand(
            deviceId,
            request.CurrentTemperature,
            trace.CorrelationId,
            trace.CausationId);
        await handler.Handle(command, cancellationToken);
        return Accepted($"/devices/{deviceId}/status");
    }

    [HttpPost("{deviceId:guid}/stop")]
    public async Task<IActionResult> Stop(
        Guid deviceId,
        [FromServices] StopDeviceCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var trace = CommandTraceHeaders.Read(Request);
        var command = new StopDeviceCommand(deviceId, trace.CorrelationId, trace.CausationId);
        await handler.Handle(command, cancellationToken);
        return Accepted($"/devices/{deviceId}/status");
    }

    [HttpPost("{deviceId:guid}/maintenance")]
    public async Task<IActionResult> SetMaintenanceMode(
        Guid deviceId,
        [FromBody] SetMaintenanceModeRequest request,
        [FromServices] SetDeviceMaintenanceModeCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var trace = CommandTraceHeaders.Read(Request);
        var command = new SetDeviceMaintenanceModeCommand(
            deviceId,
            request.Enabled,
            trace.CorrelationId,
            trace.CausationId);
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

public sealed record SetMaintenanceModeRequest(bool Enabled);
