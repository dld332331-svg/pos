namespace POS.Application.DTOs;

public record UserDto(Guid Id, string Username, string DisplayName, string Role, bool IsActive, bool IsLocked, DateTime? LastLoginAt);
public record CreateUserRequest(string Username, string Password, string DisplayName, string Role);
public record UpdateUserRequest(Guid Id, string DisplayName, string Role, bool IsActive, List<string> Permissions);