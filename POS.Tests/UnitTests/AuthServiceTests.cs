#nullable enable

using System.Linq.Expressions;
using Xunit;
using Moq;
using FluentAssertions;
using POS.Application.DTOs;
using POS.Application.Services.Implementations;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for AuthService.LoginAsync.
///
/// Scenarios (9 tests):
///   1. Empty username → failed with "مطلوبان"
///   2. Empty password → failed with "مطلوبان"
///   3. User not found → failed with "غير صحيح"
///   4. User locked → failed with "مقفل" + audit logged
///   5. User not active → failed with "معطل" + audit logged
///   6. Wrong password, under max attempts → failed + FailedLoginAttempts incremented
///   7. Wrong password on 5th attempt → locks account + audit logged + failed
///   8. Correct password → success + FailedLoginAttempts reset + LastLoginAt set + audit logged
///   9. Sequence: 4 wrong attempts, then correct on 5th → succeeds (exact max boundary)
/// </summary>
public class AuthServiceTests
{

    // ========================================================================
    // Test Data Builders
    // ========================================================================

    /// <summary>
    /// Creates an active, unlocked user with the given username and password.
    /// </summary>
    private static User CreateActiveUser(string username = "cashier1", string passwordHash = "hashed_pwd",
        UserRole role = UserRole.Cashier, int failedAttempts = 0, bool isLocked = false, bool isActive = true)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = passwordHash,
            FullName = "Test Cashier",
            Role = role,
            IsActive = isActive,
            FailedLoginAttempts = failedAttempts,
            IsLocked = isLocked,
            MustChangePassword = false
        };
    }

    // ========================================================================
    // Mock Builder
    // ========================================================================

    /// <summary>
    /// Builds an AuthService with mocked dependencies.
    /// </summary>
    /// <param name="user">The user that FindAsync will return. Null = no user found.</param>
    /// <param name="passwordValid">Whether VerifyPassword returns true (valid password).</param>
    /// <param name="canConnect">Whether CanConnectAsync returns true.</param>
    /// <param name="permissions">Permissions array returned by GetPermissionsAsync (null = empty).</param>
    private (AuthService service, Mock<IUnitOfWork> unitOfWorkMock, Mock<IAuditService> auditServiceMock,
        Mock<IPasswordHasher> passwordHasherMock, Mock<IPermissionService> permissionServiceMock)
        BuildServiceWithMocks(User? user, bool passwordValid, bool canConnect = true, string[]? permissions = null)
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var auditServiceMock = new Mock<IAuditService>();
        var passwordHasherMock = new Mock<IPasswordHasher>();
        var permissionServiceMock = new Mock<IPermissionService>();

        // Audit service — fire-and-forget, always succeeds
        auditServiceMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // Password hasher
        passwordHasherMock
            .Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(passwordValid);
        passwordHasherMock
            .Setup(p => p.HashPassword(It.IsAny<string>()))
            .Returns("new_hashed_password_value");

        // Users repository
        var userRepoMock = new Mock<IRepository<User>>();
        userRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user != null ? new List<User> { user } : new List<User>());
        userRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(user);
        userRepoMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.Users).Returns(userRepoMock.Object);

        // CanConnectAsync
        unitOfWorkMock.Setup(u => u.CanConnectAsync()).ReturnsAsync(canConnect);

        // Permission service
        permissionServiceMock
            .Setup(p => p.GetPermissionsAsync(It.IsAny<string>()))
            .ReturnsAsync(permissions ?? Array.Empty<string>());

        // SaveChanges
        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var service = new AuthService(
            unitOfWorkMock.Object,
            permissionServiceMock.Object,
            auditServiceMock.Object,
            passwordHasherMock.Object);

        return (service, unitOfWorkMock, auditServiceMock, passwordHasherMock, permissionServiceMock);
    }

    // ========================================================================
    // LoginAsync Tests
    // ========================================================================

    [Fact]
    public async Task LoginAsync_EmptyUsername_ReturnsFailed()
    {
        // Arrange
        var (service, _, _, _, _) = BuildServiceWithMocks(user: null, passwordValid: false);

        // Act
        var result = await service.LoginAsync("", "password123");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("اسم المستخدم وكلمة المرور مطلوبان");
        result.User.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_EmptyPassword_ReturnsFailed()
    {
        // Arrange
        var (service, _, _, _, _) = BuildServiceWithMocks(user: null, passwordValid: false);

        // Act
        var result = await service.LoginAsync("cashier1", "");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("اسم المستخدم وكلمة المرور مطلوبان");
        result.User.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ReturnsFailed()
    {
        // Arrange
        var (service, _, _, _, _) = BuildServiceWithMocks(user: null, passwordValid: false);

        // Act
        var result = await service.LoginAsync("nonexistent", "password123");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("اسم المستخدم أو كلمة المرور غير صحيح");
        result.User.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_UserLocked_ReturnsFailedAndLogsAudit()
    {
        // Arrange
        var user = CreateActiveUser(isLocked: true);
        var (service, _, auditServiceMock, _, _) = BuildServiceWithMocks(user, passwordValid: true);

        // Act
        var result = await service.LoginAsync(user.Username, "any_password");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("الحساب مقفل. تواصل مع المسؤول");
        result.User.Should().BeNull();

        // Audit should be logged with "Account locked"
        auditServiceMock.Verify(a => a.LogAsync(
            null,
            AuditActionType.LoginFailure,
            "User",
            user.Id,
            null,
            null,
            "Account locked"), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_UserNotActive_ReturnsFailedAndLogsAudit()
    {
        // Arrange
        var user = CreateActiveUser(isActive: false);
        var (service, _, auditServiceMock, _, _) = BuildServiceWithMocks(user, passwordValid: true);

        // Act
        var result = await service.LoginAsync(user.Username, "any_password");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("الحساب معطل. تواصل مع المسؤول");
        result.User.Should().BeNull();

        // Audit should be logged with "Account disabled"
        auditServiceMock.Verify(a => a.LogAsync(
            null,
            AuditActionType.LoginFailure,
            "User",
            user.Id,
            null,
            null,
            "Account disabled"), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WrongPasswordUnderMaxAttempts_IncrementsAndReturnsFailed()
    {
        // Arrange
        var user = CreateActiveUser(failedAttempts: 2);
        var (service, unitOfWorkMock, auditServiceMock, _, _) = BuildServiceWithMocks(user, passwordValid: false);

        // Act
        var result = await service.LoginAsync(user.Username, "wrong_password");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("اسم المستخدم أو كلمة المرور غير صحيح");
        result.User.Should().BeNull();

        // FailedLoginAttempts should be incremented to 3
        unitOfWorkMock.Verify(u => u.Users.UpdateAsync(
            It.Is<User>(u => u.FailedLoginAttempts == 3)), Times.Once);

        // Audit should log the failed attempt
        auditServiceMock.Verify(a => a.LogAsync(
            null,
            AuditActionType.LoginFailure,
            "User",
            user.Id,
            null,
            null,
            "Failed attempt 3"), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WrongPasswordOnFifthAttempt_LocksAccountAndAudits()
    {
        // Arrange — user already has 4 failed attempts; 5th will lock
        var user = CreateActiveUser(failedAttempts: 4);
        var (service, unitOfWorkMock, auditServiceMock, _, _) = BuildServiceWithMocks(user, passwordValid: false);

        // Act
        var result = await service.LoginAsync(user.Username, "wrong_password");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("تم قفل الحساب بسبب عدد محاولات فاشلة");
        result.User.Should().BeNull();

        // User should be locked with FailedLoginAttempts = 5
        unitOfWorkMock.Verify(u => u.Users.UpdateAsync(
            It.Is<User>(u =>
                u.IsLocked == true &&
                u.FailedLoginAttempts == 5)), Times.Once);

        // Audit should log the lockout
        auditServiceMock.Verify(a => a.LogAsync(
            null,
            AuditActionType.LoginFailure,
            "User",
            user.Id,
            null,
            null,
            "Account locked due to max failed attempts"), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_CorrectPassword_ReturnsSuccessResetsAttemptsAndAudits()
    {
        // Arrange — user with previous failed attempts
        var user = CreateActiveUser(failedAttempts: 2);
        var (service, unitOfWorkMock, auditServiceMock, _, _) = BuildServiceWithMocks(user, passwordValid: true);

        // Act
        var result = await service.LoginAsync(user.Username, "correct_password");

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Login successful.");
        result.User.Should().NotBeNull();
        result.User!.Id.Should().Be(user.Id);

        // FailedLoginAttempts should be reset to 0 and LastLoginAt set
        unitOfWorkMock.Verify(u => u.Users.UpdateAsync(
            It.Is<User>(u =>
                u.FailedLoginAttempts == 0 &&
                u.LastLoginAt.HasValue)), Times.Once);

        // Audit should log the success
        auditServiceMock.Verify(a => a.LogAsync(
            user.Id,
            AuditActionType.LoginSuccess,
            "User",
            user.Id,
            null,
            null,
            null), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_FourWrongAttemptsThenCorrect_SucceedsOnFifth()
    {
        // Arrange — user at max-1 (4) failed attempts, now enters correct password
        var user = CreateActiveUser(failedAttempts: 4);
        var (service, unitOfWorkMock, _, _, _) = BuildServiceWithMocks(user, passwordValid: true);

        // Act — correct password on the 5th attempt
        var result = await service.LoginAsync(user.Username, "correct_password");

        // Assert
        result.Success.Should().BeTrue();
        result.User.Should().NotBeNull();

        // FailedLoginAttempts should be reset to 0
        unitOfWorkMock.Verify(u => u.Users.UpdateAsync(
            It.Is<User>(u => u.FailedLoginAttempts == 0)), Times.Once);
    }

    // ========================================================================
    // ChangePasswordAsync Tests
    // ========================================================================

    [Fact]
    public async Task ChangePasswordAsync_Success_UpdatesPasswordAndResetsMustChangeFlag()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateActiveUser();
        user.Id = userId;
        user.MustChangePassword = true;
        user.PasswordHash = "old_hash";

        var (service, unitOfWorkMock, auditServiceMock, passwordHasherMock, _) =
            BuildServiceWithMocks(user, passwordValid: true);

        var request = new ChangePasswordRequest(userId, "old_password", "new_secure_password");

        // Act
        var result = await service.ChangePasswordAsync(request);

        // Assert
        result.Should().BeTrue();

        // Password hash should be updated using HashPassword
        passwordHasherMock.Verify(p => p.HashPassword("new_secure_password"), Times.Once);

        // User should be updated with new hash and MustChangePassword = false
        unitOfWorkMock.Verify(u => u.Users.UpdateAsync(
            It.Is<User>(u =>
                u.PasswordHash == "new_hashed_password_value" &&
                u.MustChangePassword == false)), Times.Once);

        // SaveChanges was called
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);

        // Audit was logged with "Password changed"
        auditServiceMock.Verify(a => a.LogAsync(
            userId,
            AuditActionType.UserUpdated,
            "User",
            user.Id,
            null,
            null,
            "Password changed"), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_UserNotFound_ReturnsFalse()
    {
        // Arrange — user is null
        var (service, _, _, _, _) = BuildServiceWithMocks(user: null, passwordValid: false);

        var request = new ChangePasswordRequest(Guid.NewGuid(), "old_password", "new_password");

        // Act
        var result = await service.ChangePasswordAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongOldPassword_ReturnsFalse()
    {
        // Arrange — VerifyPassword returns false (passwordValid = false)
        var userId = Guid.NewGuid();
        var user = CreateActiveUser();
        user.Id = userId;

        var (service, unitOfWorkMock, _, _, _) = BuildServiceWithMocks(user, passwordValid: false);

        var request = new ChangePasswordRequest(userId, "wrong_old_password", "new_password");

        // Act
        var result = await service.ChangePasswordAsync(request);

        // Assert
        result.Should().BeFalse();

        // User should NOT be updated (we returned early)
        unitOfWorkMock.Verify(u => u.Users.UpdateAsync(It.IsAny<User>()), Times.Never);

        // SaveChanges should NOT be called
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    // ========================================================================
    // CheckDatabaseConnectionAsync Tests
    // ========================================================================

    [Fact]
    public async Task CheckDatabaseConnectionAsync_CanConnect_ReturnsTrue()
    {
        // Arrange
        var (service, _, _, _, _) = BuildServiceWithMocks(user: null, passwordValid: false, canConnect: true);

        // Act
        var result = await service.CheckDatabaseConnectionAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckDatabaseConnectionAsync_CannotConnect_ReturnsFalse()
    {
        // Arrange
        var (service, _, _, _, _) = BuildServiceWithMocks(user: null, passwordValid: false, canConnect: false);

        // Act
        var result = await service.CheckDatabaseConnectionAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckDatabaseConnectionAsync_Exception_ReturnsFalse()
    {
        // Arrange
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock.Setup(u => u.CanConnectAsync()).ThrowsAsync(new InvalidOperationException("DB down"));
        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var authService = new AuthService(
            unitOfWorkMock.Object,
            Mock.Of<IPermissionService>(),
            Mock.Of<IAuditService>(),
            Mock.Of<IPasswordHasher>());

        // Act
        var result = await authService.CheckDatabaseConnectionAsync();

        // Assert — catch block should return false
        result.Should().BeFalse();
    }

    // ========================================================================
    // LogoutAsync Tests
    // ========================================================================

    [Fact]
    public async Task LogoutAsync_LogsAuditEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateActiveUser();
        user.Id = userId;
        var (service, _, auditServiceMock, _, _) = BuildServiceWithMocks(user, passwordValid: true);

        // Act
        await service.LogoutAsync(userId);

        // Assert
        auditServiceMock.Verify(a => a.LogAsync(
            userId,
            AuditActionType.Logout,
            "User",
            userId,
            null,
            null,
            null), Times.Once);
    }

    // ========================================================================
    // GetUserPermissionsAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetUserPermissionsAsync_UserExists_ReturnsPermissions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateActiveUser(role: UserRole.Admin);
        user.Id = userId;
        var expectedPermissions = new[] { "ManageUsers", "ViewReports", "ProcessSales" };
        var (service, _, _, _, permissionServiceMock) =
            BuildServiceWithMocks(user, passwordValid: true, permissions: expectedPermissions);

        // Act
        var result = await service.GetUserPermissionsAsync(userId);

        // Assert
        result.Should().BeEquivalentTo(expectedPermissions);
        permissionServiceMock.Verify(p => p.GetPermissionsAsync("Admin"), Times.Once);
    }

    [Fact]
    public async Task GetUserPermissionsAsync_UserNotFound_ReturnsEmptyList()
    {
        // Arrange
        var (service, _, _, _, _) = BuildServiceWithMocks(user: null, passwordValid: false);

        // Act
        var result = await service.GetUserPermissionsAsync(Guid.NewGuid());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserPermissionsAsync_NoPermissions_ReturnsEmpty()
    {
        // Arrange — user exists but has no permissions
        var userId = Guid.NewGuid();
        var user = CreateActiveUser(role: UserRole.Cashier);
        user.Id = userId;
        var (service, _, _, _, _) = BuildServiceWithMocks(user, passwordValid: true, permissions: Array.Empty<string>());

        // Act
        var result = await service.GetUserPermissionsAsync(userId);

        // Assert
        result.Should().BeEmpty();
    }

    // ========================================================================
    // HasPermissionAsync Tests
    // ========================================================================

    [Fact]
    public async Task HasPermissionAsync_UserHasPermission_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateActiveUser(role: UserRole.Manager);
        user.Id = userId;
        var permissions = new[] { "ProcessSales", "OpenCashDrawer", "VoidTransaction" };
        var (service, _, _, _, _) = BuildServiceWithMocks(user, passwordValid: true, permissions: permissions);

        // Act
        var result = await service.HasPermissionAsync(userId, "OpenCashDrawer");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_UserDoesNotHavePermission_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateActiveUser(role: UserRole.Cashier);
        user.Id = userId;
        var permissions = new[] { "ProcessSales" };
        var (service, _, _, _, _) = BuildServiceWithMocks(user, passwordValid: true, permissions: permissions);

        // Act
        var result = await service.HasPermissionAsync(userId, "ManageUsers");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_UserNotFound_ReturnsFalse()
    {
        // Arrange
        var (service, _, _, _, _) = BuildServiceWithMocks(user: null, passwordValid: false);

        // Act
        var result = await service.HasPermissionAsync(Guid.NewGuid(), "ProcessSales");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_UserNotActive_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateActiveUser(role: UserRole.Cashier, isActive: false);
        user.Id = userId;
        var (service, _, _, _, _) = BuildServiceWithMocks(user, passwordValid: true, permissions: new[] { "ProcessSales" });

        // Act
        var result = await service.HasPermissionAsync(userId, "ProcessSales");

        // Assert — even though user has the permission, inactive user should return false
        result.Should().BeFalse();
    }
}
