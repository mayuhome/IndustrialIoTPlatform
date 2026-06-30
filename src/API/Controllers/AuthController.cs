using System.Security.Claims;
using Application.Auth.Commands;
using Application.Auth.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserRequest request,
        [FromServices] RegisterUserCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new RegisterUserCommand(request.Username, request.Password), cancellationToken);
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginUserRequest request,
        [FromServices] LoginUserCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new LoginUserCommand(request.Username, request.Password), cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(
        [FromServices] GetCurrentUserQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        var result = await handler.Handle(new GetCurrentUserQuery(userId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}

public sealed record RegisterUserRequest(string Username, string Password);

public sealed record LoginUserRequest(string Username, string Password);
