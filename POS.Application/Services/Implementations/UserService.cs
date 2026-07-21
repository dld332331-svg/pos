using POS.Application.DTOs;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IUnitOfWork unitOfWork, IAuditService auditService, IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _passwordHasher = passwordHasher;
    }

    public async Task<List<UserDto>> GetUsersAsync()
    {
        var users = await _unitOfWork.Users.GetAllAsync();
        return users.Select(u => new UserDto(
            u.Id,
            u.Username,
            u.FullName,
            u.Role.ToString(),
            u.IsActive,
            u.IsLocked,
            u.LastLoginAt)).ToList();
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid id)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id);
        if (user is null) return null;

        return new UserDto(
            user.Id,
            user.Username,
            user.FullName,
            user.Role.ToString(),
            user.IsActive,
            user.IsLocked,
            user.LastLoginAt);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Check unique username
        var existing = (await _unitOfWork.Users.FindAsync(u => u.Username == request.Username)).FirstOrDefault();
        if (existing is not null)
            throw new InvalidOperationException("اسم المستخدم موجود بالفعل");

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            throw new InvalidOperationException("الدور غير صالح");

        var user = new User
        {
            Username = request.Username,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = role,
            IsActive = true,
            FailedLoginAttempts = 0,
            IsLocked = false,
            MustChangePassword = true
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(null, AuditActionType.UserCreated, "User", user.Id,
            null, $"Username={user.Username},Role={user.Role}", null);

        return new UserDto(user.Id, user.Username, user.FullName, user.Role.ToString(), true, false, null);
    }

    public async Task<UserDto> UpdateUserAsync(UpdateUserRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await _unitOfWork.Users.GetByIdAsync(request.Id)
            ?? throw new InvalidOperationException("المستخدم غير موجود");

        var beforeValue = $"FullName={user.FullName},Role={user.Role},IsActive={user.IsActive}";

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            throw new InvalidOperationException("الدور غير صالح");

        user.Role = role;
        user.IsActive = request.IsActive;
        user.MarkAsModified();

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        var afterValue = $"FullName={user.FullName},Role={user.Role},IsActive={user.IsActive}";
        await _auditService.LogAsync(null, AuditActionType.UserUpdated, "User", user.Id, beforeValue, afterValue, null);

        return new UserDto(user.Id, user.Username, user.FullName, user.Role.ToString(), user.IsActive, user.IsLocked, user.LastLoginAt);
    }

    public async Task<OperationResult> ToggleUserStatusAsync(Guid id, bool isActive)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id);
        if (user is null)
            return new OperationResult(false, ErrorMessage: "المستخدم غير موجود");

        var beforeValue = $"IsActive={user.IsActive}";
        user.IsActive = isActive;
        user.MarkAsModified();

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        var afterValue = $"IsActive={user.IsActive}";
        await _auditService.LogAsync(null, AuditActionType.UserUpdated, "User", user.Id, beforeValue, afterValue,
            isActive ? "Account activated" : "Account deactivated");

        return new OperationResult(true, SuccessMessage: isActive ? "تم تفعيل المستخدم" : "تم تعطيل المستخدم");
    }

    public async Task<OperationResult> UnlockUserAsync(Guid id)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id);
        if (user is null)
            return new OperationResult(false, ErrorMessage: "المستخدم غير موجود");

        var beforeValue = $"FailedLoginAttempts={user.FailedLoginAttempts},IsLocked={user.IsLocked}";
        user.FailedLoginAttempts = 0;
        user.IsLocked = false;
        user.MarkAsModified();

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        var afterValue = $"FailedLoginAttempts=0,IsLocked=False";
        await _auditService.LogAsync(null, AuditActionType.UserUpdated, "User", user.Id, beforeValue, afterValue, "Account unlocked");

        return new OperationResult(true, SuccessMessage: "تم فتح الحساب بنجاح");
    }

    public Task<List<string>> GetAllPermissionsAsync()
    {
        var permissions = Enum.GetNames<Permission>().ToList();
        return Task.FromResult(permissions);
    }
}