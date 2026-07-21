using POS.Application.CQRS.Abstractions;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Application.CQRS.Commands;

public record CreateUserCommand(string Username, string Password, string DisplayName, string Role, List<string> Permissions, bool IsActive) : ICommand<UserDto>;
public record UpdateUserCommand(Guid Id, string DisplayName, string Role, bool IsActive, List<string> Permissions) : ICommand<UserDto>;
public record ToggleUserStatusCommand(Guid Id, bool IsActive) : ICommand<OperationResult>;

public sealed class CreateUserCommandHandler(IUserService service) : ICommandHandler<CreateUserCommand, UserDto>
{
    public Task<UserDto> HandleAsync(CreateUserCommand cmd, CancellationToken ct = default)
        => service.CreateUserAsync(new CreateUserRequest(cmd.Username, cmd.Password, cmd.DisplayName, cmd.Role));
}

public sealed class UpdateUserCommandHandler(IUserService service) : ICommandHandler<UpdateUserCommand, UserDto>
{
    public Task<UserDto> HandleAsync(UpdateUserCommand cmd, CancellationToken ct = default)
        => service.UpdateUserAsync(new UpdateUserRequest(cmd.Id, cmd.DisplayName, cmd.Role, cmd.IsActive, cmd.Permissions));
}

public sealed class ToggleUserStatusCommandHandler(IUserService service) : ICommandHandler<ToggleUserStatusCommand, OperationResult>
{
    public Task<OperationResult> HandleAsync(ToggleUserStatusCommand cmd, CancellationToken ct = default)
        => service.ToggleUserStatusAsync(cmd.Id, cmd.IsActive);
}
