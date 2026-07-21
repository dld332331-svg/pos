using POS.Application.CQRS.Abstractions;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Application.CQRS.Queries;

public record GetUsersQuery : IQuery<List<UserDto>>;
public record GetUserByIdQuery(Guid Id) : IQuery<UserDto?>;
public record GetAllPermissionsQuery : IQuery<List<string>>;

public sealed class GetUsersQueryHandler(IUserService service) : IQueryHandler<GetUsersQuery, List<UserDto>>
{
    public Task<List<UserDto>> HandleAsync(GetUsersQuery q, CancellationToken ct = default)
        => service.GetUsersAsync();
}

public sealed class GetUserByIdQueryHandler(IUserService service) : IQueryHandler<GetUserByIdQuery, UserDto?>
{
    public Task<UserDto?> HandleAsync(GetUserByIdQuery q, CancellationToken ct = default)
        => service.GetUserByIdAsync(q.Id);
}

public sealed class GetAllPermissionsQueryHandler(IUserService service) : IQueryHandler<GetAllPermissionsQuery, List<string>>
{
    public Task<List<string>> HandleAsync(GetAllPermissionsQuery q, CancellationToken ct = default)
        => service.GetAllPermissionsAsync();
}
