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

public class UserServiceTests
{
    private static User CreateTestUser(Guid? id = null, string username = "user1", string role = "Cashier",
        bool isActive = true, bool isLocked = false, int failedAttempts = 0)
    {
        var user = new User
        {
            Id = id ?? Guid.NewGuid(),
            Username = username,
            PasswordHash = "hash",
            FullName = "Test User",
            Role = Enum.Parse<UserRole>(role),
            IsActive = isActive,
            IsLocked = isLocked,
            FailedLoginAttempts = failedAttempts,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        return user;
    }

    private static Mock<IRepository<T>> CreateEmptyRepoMock<T>() where T : BaseEntity
    {
        var mock = new Mock<IRepository<T>>();
        mock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<T, bool>>>())).ReturnsAsync(new List<T>());
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<T>());
        return mock;
    }

    private (UserService service, Mock<IUnitOfWork> uowMock, Mock<IAuditService> auditMock, Mock<IPasswordHasher> hasherMock)
        BuildService(List<User>? users = null)
    {
        var uowMock = new Mock<IUnitOfWork>();
        var auditMock = new Mock<IAuditService>();
        var hasherMock = new Mock<IPasswordHasher>();

        auditMock.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
            It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>())).Returns(Task.CompletedTask);
        hasherMock.Setup(h => h.HashPassword(It.IsAny<string>())).Returns("hashed");
        uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var userRepo = new Mock<IRepository<User>>();
        userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(users ?? new List<User>());
        userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => users?.FirstOrDefault(u => u.Id == id));
        userRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync((Expression<Func<User, bool>> predicate) =>
                (users ?? new List<User>()).AsQueryable().Where(predicate).ToList());
        userRepo.Setup(r => r.AddAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
        userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
        uowMock.Setup(u => u.Users).Returns(userRepo.Object);

        // Stub remaining repos
        uowMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        uowMock.Setup(u => u.Products).Returns(CreateEmptyRepoMock<Product>().Object);
        uowMock.Setup(u => u.InventoryItems).Returns(CreateEmptyRepoMock<InventoryItem>().Object);
        uowMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        uowMock.Setup(u => u.Sales).Returns(CreateEmptyRepoMock<Sale>().Object);
        uowMock.Setup(u => u.SaleItems).Returns(CreateEmptyRepoMock<SaleItem>().Object);
        uowMock.Setup(u => u.Payments).Returns(CreateEmptyRepoMock<Payment>().Object);
        uowMock.Setup(u => u.Shifts).Returns(CreateEmptyRepoMock<Shift>().Object);
        uowMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        uowMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        uowMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        uowMock.Setup(u => u.Expenses).Returns(CreateEmptyRepoMock<Expense>().Object);
        uowMock.Setup(u => u.WithdrawalDeposits).Returns(CreateEmptyRepoMock<WithdrawalDeposit>().Object);
        uowMock.Setup(u => u.Printers).Returns(CreateEmptyRepoMock<Printer>().Object);
        uowMock.Setup(u => u.Registers).Returns(CreateEmptyRepoMock<Register>().Object);
        uowMock.Setup(u => u.KitchenStations).Returns(CreateEmptyRepoMock<KitchenStation>().Object);
        uowMock.Setup(u => u.Rooms).Returns(CreateEmptyRepoMock<Room>().Object);
        uowMock.Setup(u => u.Suppliers).Returns(CreateEmptyRepoMock<Supplier>().Object);

        var service = new UserService(uowMock.Object, auditMock.Object, hasherMock.Object);
        return (service, uowMock, auditMock, hasherMock);
    }

    [Fact] public async Task GetUsersAsync_ReturnsAllUsers()
    {
        var users = new List<User> { CreateTestUser(), CreateTestUser(username: "admin", role: "Admin") };
        var (service, _, _, _) = BuildService(users);
        var result = await service.GetUsersAsync();
        result.Should().HaveCount(2);
    }

    [Fact] public async Task GetUserByIdAsync_Found_ReturnsDto()
    {
        var id = Guid.NewGuid();
        var user = CreateTestUser(id);
        var (service, _, _, _) = BuildService(new List<User> { user });
        var result = await service.GetUserByIdAsync(id);
        result.Should().NotBeNull();
        result!.Username.Should().Be("user1");
    }

    [Fact] public async Task GetUserByIdAsync_NotFound_ReturnsNull()
    {
        var (service, _, _, _) = BuildService();
        var result = await service.GetUserByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact] public async Task CreateUserAsync_Success_CreatesUserAndLogsAudit()
    {
        var (service, uowMock, auditMock, hasherMock) = BuildService();
        var request = new CreateUserRequest("newuser", "pass123", "New User", "Cashier");
        var result = await service.CreateUserAsync(request);
        result.Username.Should().Be("newuser");
        hasherMock.Verify(h => h.HashPassword("pass123"), Times.Once);
        uowMock.Verify(u => u.Users.AddAsync(It.IsAny<User>()), Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);
        auditMock.Verify(a => a.LogAsync(null, AuditActionType.UserCreated, "User", It.IsAny<Guid>(),
            null, It.Is<string>(s => s.Contains("newuser")), null), Times.Once);
    }

    [Fact] public async Task CreateUserAsync_DuplicateUsername_Throws()
    {
        var existing = CreateTestUser(username: "dupuser");
        var (service, _, _, _) = BuildService(new List<User> { existing });
        var request = new CreateUserRequest("dupuser", "pass", "Dup", "Cashier");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateUserAsync(request));
    }

    [Fact] public async Task CreateUserAsync_InvalidRole_Throws()
    {
        var (service, _, _, _) = BuildService();
        var request = new CreateUserRequest("u", "p", "U", "InvalidRole");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateUserAsync(request));
    }

    [Fact] public async Task UpdateUserAsync_Success_UpdatesAndLogsAudit()
    {
        var id = Guid.NewGuid();
        var user = CreateTestUser(id, role: "Cashier");
        var (service, uowMock, auditMock, _) = BuildService(new List<User> { user });
        var request = new UpdateUserRequest(id, "Updated User", "Admin", true, new List<string>());
        var result = await service.UpdateUserAsync(request);
        result.Role.Should().Be("Admin");
        uowMock.Verify(u => u.Users.UpdateAsync(user), Times.Once);
        auditMock.Verify(a => a.LogAsync(null, AuditActionType.UserUpdated, "User", id,
            It.IsAny<string>(), It.IsAny<string>(), null), Times.Once);
    }

    [Fact] public async Task UpdateUserAsync_NotFound_Throws()
    {
        var (service, _, _, _) = BuildService();
        var request = new UpdateUserRequest(Guid.NewGuid(), "X", "Cashier", true, new List<string>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateUserAsync(request));
    }

    [Fact] public async Task ToggleUserStatusAsync_Success_TogglesAndLogs()
    {
        var id = Guid.NewGuid();
        var user = CreateTestUser(id, isActive: true);
        var (service, _, auditMock, _) = BuildService(new List<User> { user });
        var result = await service.ToggleUserStatusAsync(id, false);
        result.Success.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        auditMock.Verify(a => a.LogAsync(null, AuditActionType.UserUpdated, "User", id,
            "IsActive=True", "IsActive=False", "Account deactivated"), Times.Once);
    }

    [Fact] public async Task ToggleUserStatusAsync_NotFound_ReturnsFailure()
    {
        var (service, _, _, _) = BuildService();
        var result = await service.ToggleUserStatusAsync(Guid.NewGuid(), false);
        result.Success.Should().BeFalse();
    }

    [Fact] public async Task UnlockUserAsync_Success_ResetsAttemptsAndLogs()
    {
        var id = Guid.NewGuid();
        var user = CreateTestUser(id, isLocked: true, failedAttempts: 3);
        var (service, _, auditMock, _) = BuildService(new List<User> { user });
        var result = await service.UnlockUserAsync(id);
        result.Success.Should().BeTrue();
        user.IsLocked.Should().BeFalse();
        user.FailedLoginAttempts.Should().Be(0);
        auditMock.Verify(a => a.LogAsync(null, AuditActionType.UserUpdated, "User", id,
            It.Is<string>(s => s.Contains("FailedLoginAttempts=3")),
            It.Is<string>(s => s.Contains("FailedLoginAttempts=0")), "Account unlocked"), Times.Once);
    }

    [Fact] public async Task UnlockUserAsync_NotFound_ReturnsFailure()
    {
        var (service, _, _, _) = BuildService();
        var result = await service.UnlockUserAsync(Guid.NewGuid());
        result.Success.Should().BeFalse();
    }

    [Fact] public async Task GetAllPermissionsAsync_ReturnsAllPermissionNames()
    {
        var (service, _, _, _) = BuildService();
        var result = await service.GetAllPermissionsAsync();
        result.Should().NotBeEmpty();
        result.Should().Contain("Sell");
        result.Should().Contain("ViewReports");
        result.Should().Contain("ManageUsers");
    }
}
