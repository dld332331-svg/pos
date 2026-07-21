namespace POS.Application.DTOs;

public record LoginRequest(string Username, string Password);
public record LoginResponse(Guid UserId, string DisplayName, string Role, bool MustChangePassword, List<string> Permissions);
public record ChangePasswordRequest(Guid UserId, string OldPassword, string NewPassword);