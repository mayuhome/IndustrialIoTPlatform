namespace Application.Auth.Models;

public sealed record UserProfileView(
    Guid UserId,
    string Username,
    string Role,
    DateTime CreatedAtUtc);
