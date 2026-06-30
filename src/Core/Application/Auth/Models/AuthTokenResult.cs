namespace Application.Auth.Models;

public sealed record AuthTokenResult(
    string AccessToken,
    DateTime ExpiresAtUtc,
    Guid UserId,
    string Username,
    string Role);
