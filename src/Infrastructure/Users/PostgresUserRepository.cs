using Application.Abstractions;
using Domain;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Users;

public sealed class PostgresUserRepository(InfrastructureDbContext dbContext) : IUserRepository
{
    private readonly InfrastructureDbContext _dbContext = dbContext;

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var normalizedUsername = username.Trim().ToUpperInvariant();
        var user = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.NormalizedUsername == normalizedUsername, cancellationToken);

        return user is null ? null : MapUser(user);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return user is null ? null : MapUser(user);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        var entity = new UserRecord
        {
            Id = user.Id,
            Username = user.Username,
            NormalizedUsername = user.NormalizedUsername,
            PasswordHash = user.PasswordHash,
            Role = user.Role.ToString(),
            CreatedAtUtc = user.CreatedAtUtc
        };

        _dbContext.Users.Add(entity);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Username already exists.", ex);
        }
    }

    private static User MapUser(UserRecord user)
    {
        var role = Enum.Parse<UserRole>(user.Role, ignoreCase: true);
        return User.Restore(
            user.Id,
            user.Username,
            user.NormalizedUsername,
            user.PasswordHash,
            role,
            user.CreatedAtUtc);
    }
}
