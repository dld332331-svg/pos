using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IUserService
{
    Task<List<UserDto>> GetUsersAsync();
    Task<UserDto?> GetUserByIdAsync(Guid id);
    Task<UserDto> CreateUserAsync(CreateUserRequest request);
    Task<UserDto> UpdateUserAsync(UpdateUserRequest request);
    Task<OperationResult> ToggleUserStatusAsync(Guid id, bool isActive);
    Task<OperationResult> UnlockUserAsync(Guid id);
    Task<List<string>> GetAllPermissionsAsync();
}