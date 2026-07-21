using POS.Application.CQRS.Abstractions;
using POS.Application.Services;

namespace POS.Application.CQRS.Commands;

public record LoginCommand(string Username, string Password) : ICommand<Domain.Interfaces.AuthResult>;
public record LogoutCommand(Guid UserId) : ICommand;

public sealed class LoginCommandHandler(IAuthService authService) : ICommandHandler<LoginCommand, Domain.Interfaces.AuthResult>
{
    public Task<Domain.Interfaces.AuthResult> HandleAsync(LoginCommand cmd, CancellationToken ct = default)
        => authService.LoginAsync(cmd.Username, cmd.Password);
}

public sealed class LogoutCommandHandler(IAuthService authService) : ICommandHandler<LogoutCommand>
{
    public Task HandleAsync(LogoutCommand cmd, CancellationToken ct = default)
        => authService.LogoutAsync(cmd.UserId);
}
