using Domain.Enums;
using Domain.Exceptions;

namespace Domain;

public sealed class User
{
    private User(
        Guid id,
        string username,
        string normalizedUsername,
        string passwordHash,
        UserRole role,
        DateTime createdAtUtc)
    {
        Id = id;
        Username = username;
        NormalizedUsername = normalizedUsername;
        PasswordHash = passwordHash;
        Role = role;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }

    public string Username { get; }

    public string NormalizedUsername { get; }

    public string PasswordHash { get; }

    public UserRole Role { get; }

    public DateTime CreatedAtUtc { get; }

    public static User Register(string username, string passwordHash, UserRole role = UserRole.Operator)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new DomainException("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        var trimmedUsername = username.Trim();
        if (trimmedUsername.Length < 3)
        {
            throw new DomainException("Username must be at least 3 characters long.");
        }

        return new User(
            Guid.NewGuid(),
            trimmedUsername,
            trimmedUsername.ToUpperInvariant(),
            passwordHash,
            role,
            DateTime.UtcNow);
    }

    public static User Restore(
        Guid id,
        string username,
        string normalizedUsername,
        string passwordHash,
        UserRole role,
        DateTime createdAtUtc)
    {
        return new User(id, username, normalizedUsername, passwordHash, role, createdAtUtc);
    }
}
