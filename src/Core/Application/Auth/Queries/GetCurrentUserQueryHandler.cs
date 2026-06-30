using Application.Abstractions;
using Application.Auth.Models;

namespace Application.Auth.Queries;

public sealed class GetCurrentUserQueryHandler(IUserRepository userRepository)
{
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<UserProfileView?> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(query.UserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        return new UserProfileView(user.Id, user.Username, user.Role.ToString(), user.CreatedAtUtc);
    }
}
