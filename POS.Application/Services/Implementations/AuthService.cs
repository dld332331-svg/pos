using POS.Application.DTOs;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class AuthService : IAuthService
{
    private const int MaxFailedAttempts = 5;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPermissionService _permissionService;
    private readonly IAuditService _auditService;
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(IUnitOfWork unitOfWork, IPermissionService permissionService, IAuditService auditService, IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _permissionService = permissionService;
        _auditService = auditService;
        _passwordHasher = passwordHasher;
    }

    /// <inheritdoc />
    public async Task<bool> CheckDatabaseConnectionAsync()
    {
        try
        {
            return await _unitOfWork.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return AuthResult.Failed("اسم المستخدم وكلمة المرور مطلوبان");

        var user = (await _unitOfWork.Users.FindAsync(u => u.Username == username)).FirstOrDefault();

        if (user is null)
            return AuthResult.Failed("اسم المستخدم أو كلمة المرور غير صحيح");

        if (user.IsLocked)
        {
            await _auditService.LogAsync(null, AuditActionType.LoginFailure, "User", user.Id, null, null, "Account locked");
            return AuthResult.Failed("الحساب مقفل. تواصل مع المسؤول");
        }

        if (!user.IsActive)
        {
            await _auditService.LogAsync(null, AuditActionType.LoginFailure, "User", user.Id, null, null, "Account disabled");
            return AuthResult.Failed("الحساب معطل. تواصل مع المسؤول");
        }

        if (!_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.IsLocked = true;
                await _unitOfWork.Users.UpdateAsync(user);
                await _unitOfWork.SaveChangesAsync();
                await _auditService.LogAsync(null, AuditActionType.LoginFailure, "User", user.Id, null, null, "Account locked due to max failed attempts");
                return AuthResult.Failed("تم قفل الحساب بسبب عدد محاولات فاشلة");
            }

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
            await _auditService.LogAsync(null, AuditActionType.LoginFailure, "User", user.Id, null, null, $"Failed attempt {user.FailedLoginAttempts}");
            return AuthResult.Failed("اسم المستخدم أو كلمة المرور غير صحيح");
        }

        // Successful login
        user.FailedLoginAttempts = 0;
        user.LastLoginAt = DateTime.UtcNow;

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(user.Id, AuditActionType.LoginSuccess, "User", user.Id, null, null, null);

        return AuthResult.Succeeded(user);
    }

    public async Task LogoutAsync(Guid userId)
    {
        await _auditService.LogAsync(userId, AuditActionType.Logout, "User", userId, null, null, null);
    }

    public async Task<bool> ChangePasswordAsync(ChangePasswordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId);
        if (user is null) return false;

        if (!_passwordHasher.VerifyPassword(request.OldPassword, user.PasswordHash))
            return false;

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        user.MarkAsModified(request.UserId);

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(request.UserId, AuditActionType.UserUpdated, "User", user.Id, null, null, "Password changed");

        return true;
    }

    public async Task<List<string>> GetUserPermissionsAsync(Guid userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user is null) return new List<string>();

        var permissions = await _permissionService.GetPermissionsAsync(user.Role.ToString());
        return permissions.ToList();
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permission)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user is null || !user.IsActive) return false;

        var permissions = await _permissionService.GetPermissionsAsync(user.Role.ToString());
        return permissions.Contains(permission);
    }
}