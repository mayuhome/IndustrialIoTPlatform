using Application.Abstractions;
using Application.Auth.Models;

namespace Application.Auth.Commands;

public sealed class LoginUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer)
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly ITokenIssuer _tokenIssuer = tokenIssuer;

    public async Task<AuthTokenResult> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByUsernameAsync(command.Username, cancellationToken);
        if (user is null || !_passwordHasher.VerifyPassword(command.Password, user.PasswordHash))
        {
            throw new InvalidOperationException("Invalid username or password.");
        }

        return _tokenIssuer.Issue(user);
    }
}
