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
/// Unit tests for AuditQueryService covering GetAuditLogsAsync:
/// date filtering, action type filtering, entity name filtering,
/// pagination, user name resolution, empty results.
/// </summary>
public class AuditQueryServiceTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static readonly Guid DefaultUserId = Guid.NewGuid();

    private static AuditLog CreateAuditLog(
        Guid? id = null,
        DateTime? timestamp = null,
        Guid? userId = null,
        AuditActionType actionType = AuditActionType.SaleCompleted,
        string entityName = "Sale",
        Guid? entityId = null,
        string? beforeValue = null,
        string? afterValue = null,
        string? reason = null)
    {
        return new AuditLog
        {
            Id = id ?? Guid.NewGuid(),
            Timestamp = timestamp ?? DateTime.UtcNow,
            UserId = userId,
            ActionType = actionType,
            EntityName = entityName,
            EntityId = entityId,
            BeforeValue = beforeValue,
            AfterValue = afterValue,
            Reason = reason
        };
    }

    private static User CreateTestUser(Guid id, string fullName = "مستخدم تجريبي")
    {
        return new User
        {
            Id = id,
            Username = "user",
            FullName = fullName,
            Role = UserRole.Cashier
        };
    }

    // ========================================================================
    // Mock Builder
    // ========================================================================

    private static Mock<IRepository<T>> CreateEmptyRepoMock<T>() where T : BaseEntity
    {
        var mock = new Mock<IRepository<T>>();
        mock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<T, bool>>>())).ReturnsAsync(new List<T>());
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<T>());
        return mock;
    }

    private (AuditQueryService service, Mock<IUnitOfWork> uowMock)
        BuildServiceWithMocks(
            List<AuditLog>? auditLogs = null,
            List<User>? users = null)
    {
        var uowMock = new Mock<IUnitOfWork>();

        uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // ---- AuditLogs repository (ISimpleRepository, not BaseEntity) ----
        var auditLogRepoMock = new Mock<ISimpleRepository<AuditLog>>();
        auditLogRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync((IReadOnlyList<AuditLog>)(auditLogs ?? new List<AuditLog>()));
        uowMock.Setup(u => u.AuditLogs).Returns(auditLogRepoMock.Object);

        // ---- Users repository ----
        var userRepoMock = new Mock<IRepository<User>>();
        userRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(users ?? new List<User>());
        uowMock.Setup(u => u.Users).Returns(userRepoMock.Object);

        // ---- Stub remaining repos ----
        uowMock.Setup(u => u.Products).Returns(CreateEmptyRepoMock<Product>().Object);
        uowMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
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
        uowMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);

        // BackupRecords is ISimpleRepository, not BaseEntity — create directly
        var backupRepoMock = new Mock<ISimpleRepository<BackupRecord>>();
        backupRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<BackupRecord>());
        uowMock.Setup(u => u.BackupRecords).Returns(backupRepoMock.Object);

        var service = new AuditQueryService(uowMock.Object);
        return (service, uowMock);
    }

    // ========================================================================
    // GetAuditLogsAsync — Audit Log Queries
    // ========================================================================

    [Fact]
    public async Task GetAuditLogsAsync_NoFilters_ReturnsAllPaginated()
    {
        // Arrange
        var logs = Enumerable.Range(1, 10)
            .Select(i => CreateAuditLog(
                timestamp: DateTime.UtcNow.AddMinutes(-i),
                entityName: "Sale"))
            .ToList();
        var (service, _) = BuildServiceWithMocks(auditLogs: logs);

        // Act — page 1, pageSize 5
        var result = await service.GetAuditLogsAsync(null, null, null, null, page: 1, pageSize: 5);

        // Assert
        result.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(10);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(5);

        // Ordered by timestamp descending
        result.Items[0].Timestamp.Should().BeAfter(result.Items[1].Timestamp);
    }

    [Fact]
    public async Task GetAuditLogsAsync_FilterByDate_RestrictsRange()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var logs = new List<AuditLog>
        {
            CreateAuditLog(timestamp: today.AddDays(-5)),
            CreateAuditLog(timestamp: today),
            CreateAuditLog(timestamp: today.AddDays(5))
        };
        var (service, _) = BuildServiceWithMocks(auditLogs: logs);

        // Act — filter to today
        var result = await service.GetAuditLogsAsync(today, today, null, null);

        // Assert
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAuditLogsAsync_FilterByActionType_FiltersResults()
    {
        // Arrange
        var logs = new List<AuditLog>
        {
            CreateAuditLog(actionType: AuditActionType.SaleCompleted),
            CreateAuditLog(actionType: AuditActionType.PaymentProcessed),
            CreateAuditLog(actionType: AuditActionType.SaleCompleted)
        };
        var (service, _) = BuildServiceWithMocks(auditLogs: logs);

        // Act
        var result = await service.GetAuditLogsAsync(null, null, "SALECOMPLETED", null);

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Items.Should().AllSatisfy(l => l.ActionType.Should().Be("SaleCompleted"));
    }

    [Fact]
    public async Task GetAuditLogsAsync_FilterByEntityName_FiltersResults()
    {
        // Arrange
        var logs = new List<AuditLog>
        {
            CreateAuditLog(entityName: "Sale"),
            CreateAuditLog(entityName: "Product"),
            CreateAuditLog(entityName: "Sale")
        };
        var (service, _) = BuildServiceWithMocks(auditLogs: logs);

        // Act
        var result = await service.GetAuditLogsAsync(null, null, null, "PRODUCT");

        // Assert
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.Items.First().EntityName.Should().Be("Product");
    }

    [Fact]
    public async Task GetAuditLogsAsync_ResolvesUserNameFromUserId()
    {
        // Arrange
        var userId = DefaultUserId;
        var user = CreateTestUser(userId, fullName: "أحمد المدير");
        var log = CreateAuditLog(userId: userId);
        var (service, _) = BuildServiceWithMocks(
            auditLogs: new List<AuditLog> { log },
            users: new List<User> { user });

        // Act
        var result = await service.GetAuditLogsAsync(null, null, null, null);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].UserName.Should().Be("أحمد المدير");
    }

    [Fact]
    public async Task GetAuditLogsAsync_NullUserId_ShowsSystem()
    {
        // Arrange
        var log = CreateAuditLog(userId: null);
        var (service, _) = BuildServiceWithMocks(
            auditLogs: new List<AuditLog> { log });

        // Act
        var result = await service.GetAuditLogsAsync(null, null, null, null);

        // Assert
        result.Items[0].UserName.Should().Be("System");
    }

    [Fact]
    public async Task GetAuditLogsAsync_UnknownUserId_ShowsSystem()
    {
        // Arrange
        var log = CreateAuditLog(userId: Guid.NewGuid()); // no matching user
        var (service, _) = BuildServiceWithMocks(
            auditLogs: new List<AuditLog> { log });

        // Act
        var result = await service.GetAuditLogsAsync(null, null, null, null);

        // Assert
        result.Items[0].UserName.Should().Be("System");
    }

    [Fact]
    public async Task GetAuditLogsAsync_EmptyLogs_ReturnsEmpty()
    {
        // Arrange
        var (service, _) = BuildServiceWithMocks();

        // Act
        var result = await service.GetAuditLogsAsync(null, null, null, null);

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAuditLogsAsync_Pagination_RespectsPageOffset()
    {
        // Arrange — 8 logs, page 2 with pageSize 3
        var logs = Enumerable.Range(1, 8)
            .Select(i => CreateAuditLog(
                timestamp: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();
        var (service, _) = BuildServiceWithMocks(auditLogs: logs);

        // Act — page 2, pageSize 3 → should return items 4,5,6 (0-indexed 3,4,5)
        var result = await service.GetAuditLogsAsync(null, null, null, null, page: 2, pageSize: 3);

        // Assert
        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(8);
        result.Page.Should().Be(2);

        // Page 3 with pageSize 3 → should return items 7,8 (0-indexed 6,7)
        var page3 = await service.GetAuditLogsAsync(null, null, null, null, page: 3, pageSize: 3);
        page3.Items.Should().HaveCount(2);
        page3.Page.Should().Be(3);
    }
}
