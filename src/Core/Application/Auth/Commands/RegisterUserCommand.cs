using Application.Abstractions;

namespace Application.Auth.Commands;

public sealed record RegisterUserCommand(
	string Username,
	string Password,
	Guid CorrelationId,
	Guid? CausationId) : ITraceableCommand
{
	public RegisterUserCommand(string Username, string Password)
		: this(Username, Password, Guid.NewGuid(), null)
	{
	}
}
