using Application.Abstractions;

namespace Application.Auth.Commands;

public sealed record LoginUserCommand(
	string Username,
	string Password,
	Guid CorrelationId,
	Guid? CausationId) : ITraceableCommand
{
	public LoginUserCommand(string Username, string Password)
		: this(Username, Password, Guid.NewGuid(), null)
	{
	}
}
