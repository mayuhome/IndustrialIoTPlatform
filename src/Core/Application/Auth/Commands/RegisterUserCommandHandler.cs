using Application.Abstractions;
using Application.Auth.Models;
using Domain;

namespace Application.Auth.Commands;

public sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer)
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly ITokenIssuer _tokenIssuer = tokenIssuer;

    public async Task<AuthTokenResult> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var existing = await _userRepository.GetByUsernameAsync(command.Username, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException("Username already exists.");
        }

        var passwordHash = _passwordHasher.HashPassword(command.Password);
        var user = User.Register(command.Username, passwordHash);

        await _userRepository.AddAsync(user, cancellationToken);
        return _tokenIssuer.Issue(user);
    }
}
