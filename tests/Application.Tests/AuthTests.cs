using Application.Abstractions;
using Application.Auth.Commands;
using Application.Auth.Models;
using Application.Auth.Queries;
using Domain;
using Domain.Enums;

namespace Application.Tests;

public class AuthTests
{
    [Fact]
    public async Task Register_Should_Create_User_And_Return_Token()
    {
        var repo = new FakeUserRepository();
        var handler = new RegisterUserCommandHandler(repo, new FakePasswordHasher(), new FakeTokenIssuer());

        var result = await handler.Handle(new RegisterUserCommand("operator1", "secret123"), CancellationToken.None);

        Assert.Equal("token-operator1", result.AccessToken);
        var saved = await repo.GetByUsernameAsync("operator1", CancellationToken.None);
        Assert.NotNull(saved);
    }

    [Fact]
    public async Task Login_Should_Throw_For_Invalid_Password()
    {
        var repo = new FakeUserRepository();
        var hasher = new FakePasswordHasher();
        var user = User.Register("operator2", hasher.HashPassword("secret123"), UserRole.Operator);
        await repo.AddAsync(user, CancellationToken.None);
        var handler = new LoginUserCommandHandler(repo, hasher, new FakeTokenIssuer());

        var act = () => handler.Handle(new LoginUserCommand("operator2", "bad-password"), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task GetCurrentUser_Should_Return_Profile_When_User_Exists()
    {
        var repo = new FakeUserRepository();
        var user = User.Register("admin1", "hashed-password", UserRole.Admin);
        await repo.AddAsync(user, CancellationToken.None);
        var handler = new GetCurrentUserQueryHandler(repo);

        var result = await handler.Handle(new GetCurrentUserQuery(user.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("admin1", result!.Username);
        Assert.Equal("Admin", result.Role);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<Guid, User> _usersById = new();
        private readonly Dictionary<string, User> _usersByName = new(StringComparer.OrdinalIgnoreCase);

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
        {
            _usersByName.TryGetValue(username.Trim(), out var user);
            return Task.FromResult(user);
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            _usersById.TryGetValue(id, out var user);
            return Task.FromResult(user);
        }

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            _usersById[user.Id] = user;
            _usersByName[user.Username] = user;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => $"hash::{password}";

        public bool VerifyPassword(string password, string passwordHash) => passwordHash == HashPassword(password);
    }

    private sealed class FakeTokenIssuer : ITokenIssuer
    {
        public AuthTokenResult Issue(User user)
        {
            return new AuthTokenResult($"token-{user.Username}", DateTime.UtcNow.AddHours(1), user.Id, user.Username, user.Role.ToString());
        }
    }
}
