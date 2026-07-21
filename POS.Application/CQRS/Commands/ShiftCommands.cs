using POS.Application.CQRS.Abstractions;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Application.CQRS.Commands;

public record OpenShiftCommand(Guid UserId, Guid RegisterId, decimal OpeningCash) : ICommand<ShiftDto>;
public record CloseShiftCommand(Guid ShiftId, decimal ActualCash, Guid UserId) : ICommand<ShiftDto>;

public sealed class OpenShiftCommandHandler(IShiftService service) : ICommandHandler<OpenShiftCommand, ShiftDto>
{
    public Task<ShiftDto> HandleAsync(OpenShiftCommand cmd, CancellationToken ct = default)
        => service.OpenShiftAsync(new OpenShiftRequest(cmd.OpeningCash, cmd.RegisterId), cmd.UserId);
}

public sealed class CloseShiftCommandHandler(IShiftService service) : ICommandHandler<CloseShiftCommand, ShiftDto>
{
    public Task<ShiftDto> HandleAsync(CloseShiftCommand cmd, CancellationToken ct = default)
        => service.CloseShiftAsync(new CloseShiftRequest(cmd.ShiftId, cmd.ActualCash), cmd.UserId);
}
