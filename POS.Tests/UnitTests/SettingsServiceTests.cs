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
/// Unit tests for SettingsService covering all 3 public methods:
/// GetSettingAsync, SetSettingAsync, GetSettingsByCategoryAsync.
/// </summary>
public class SettingsServiceTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static Setting CreateTestSetting(
        Guid? id = null,
        string key = "Tax.DefaultRate",
        string value = "0.16",
        string category = "Tax")
    {
        return new Setting
        {
            Id = id ?? Guid.NewGuid(),
            Key = key,
            Value = value,
            Category = category
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

    private (SettingsService service, Mock<IUnitOfWork> uowMock, Mock<IAuditService> auditMock)
        BuildServiceWithMocks(List<Setting>? settings = null)
    {
        var uowMock = new Mock<IUnitOfWork>();
        var auditMock = new Mock<IAuditService>();

        auditMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // ---- Settings repository ----
        var settingRepoMock = new Mock<IRepository<Setting>>();
        settingRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Setting, bool>>>()))
            .ReturnsAsync((Expression<Func<Setting, bool>> predicate) =>
                (settings ?? new List<Setting>()).AsQueryable().Where(predicate).ToList());
        settingRepoMock.Setup(r => r.AddAsync(It.IsAny<Setting>())).Returns(Task.CompletedTask);
        settingRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Setting>())).Returns(Task.CompletedTask);
        uowMock.Setup(u => u.Settings).Returns(settingRepoMock.Object);

        // ---- Stub remaining repos ----
        uowMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        uowMock.Setup(u => u.Products).Returns(CreateEmptyRepoMock<Product>().Object);
        uowMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        uowMock.Setup(u => u.InventoryItems).Returns(CreateEmptyRepoMock<InventoryItem>().Object);
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

        var service = new SettingsService(uowMock.Object, auditMock.Object);
        return (service, uowMock, auditMock);
    }

    // ========================================================================
    // GetSettingAsync — Retrieve Setting by Key
    // ========================================================================

    [Fact]
    public async Task GetSettingAsync_KeyExists_ReturnsValue()
    {
        // Arrange
        var settings = new List<Setting>
        {
            CreateTestSetting(key: "Tax.DefaultRate", value: "0.16")
        };
        var (service, _, _) = BuildServiceWithMocks(settings);

        // Act
        var result = await service.GetSettingAsync("Tax.DefaultRate");

        // Assert
        result.Should().Be("0.16");
    }

    [Fact]
    public async Task GetSettingAsync_KeyNotFound_ReturnsNull()
    {
        // Arrange
        var settings = new List<Setting>
        {
            CreateTestSetting(key: "Tax.DefaultRate", value: "0.16")
        };
        var (service, _, _) = BuildServiceWithMocks(settings);

        // Act
        var result = await service.GetSettingAsync("Nonexistent.Key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSettingAsync_EmptyKey_ReturnsNull()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.GetSettingAsync("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSettingAsync_WhitespaceKey_ReturnsNull()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.GetSettingAsync("   ");

        // Assert
        result.Should().BeNull();
    }

    // ========================================================================
    // SetSettingAsync — Create or Update Setting
    // ========================================================================

    [Fact]
    public async Task SetSettingAsync_CreateNew_AddsSetting()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (service, uowMock, auditMock) = BuildServiceWithMocks();

        // Act
        var result = await service.SetSettingAsync("Tax.DefaultRate", "0.18", userId);

        // Assert
        result.Success.Should().BeTrue();
        result.SuccessMessage.Should().Be("تم حفظ الإعداد بنجاح");

        uowMock.Verify(u => u.Settings.AddAsync(
            It.Is<Setting>(s =>
                s.Key == "Tax.DefaultRate" &&
                s.Value == "0.18" &&
                s.Category == "Tax")), Times.Once);

        uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);

        auditMock.Verify(a => a.LogAsync(userId, AuditActionType.SettingChanged, "Setting",
            It.IsAny<Guid>(), null, "0.18", null), Times.Once);
    }

    [Fact]
    public async Task SetSettingAsync_UpdateExisting_ModifiesValue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingId = Guid.NewGuid();
        var existing = CreateTestSetting(existingId, key: "Tax.DefaultRate", value: "0.16");
        var (service, uowMock, auditMock) = BuildServiceWithMocks(
            new List<Setting> { existing });

        // Act
        var result = await service.SetSettingAsync("Tax.DefaultRate", "0.18", userId);

        // Assert
        result.Success.Should().BeTrue();

        // Existing setting was updated
        existing.Value.Should().Be("0.18");

        uowMock.Verify(u => u.Settings.UpdateAsync(existing), Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);

        auditMock.Verify(a => a.LogAsync(userId, AuditActionType.SettingChanged, "Setting",
            existingId, "0.16", "0.18", null), Times.Once);
    }

    [Fact]
    public async Task SetSettingAsync_EmptyKey_ReturnsFailure()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.SetSettingAsync("", "value", Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("مفتاح الإعداد مطلوب");
    }

    [Fact]
    public async Task SetSettingAsync_NewSetting_CategoryInferredFromKey()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (service, uowMock, _) = BuildServiceWithMocks();

        // Act — key without dot separator
        var result = await service.SetSettingAsync("StoreName", "متجري", userId);

        // Assert — category is "General" for keys without dots
        result.Success.Should().BeTrue();

        uowMock.Verify(u => u.Settings.AddAsync(
            It.Is<Setting>(s => s.Category == "General")), Times.Once);
    }

    // ========================================================================
    // GetSettingsByCategoryAsync — Category-based Retrieval
    // ========================================================================

    [Fact]
    public async Task GetSettingsByCategoryAsync_ReturnsCategorySettings()
    {
        // Arrange
        var settings = new List<Setting>
        {
            CreateTestSetting(key: "Tax.DefaultRate", value: "0.16", category: "Tax"),
            CreateTestSetting(key: "Tax.SecondRate", value: "0.05", category: "Tax"),
            CreateTestSetting(key: "Store.Name", value: "متجر", category: "Store"),
            CreateTestSetting(key: "Print.Header", value: "فاتورة", category: "Print")
        };
        var (service, _, _) = BuildServiceWithMocks(settings);

        // Act
        var result = await service.GetSettingsByCategoryAsync("Tax");

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainKey("Tax.DefaultRate").WhoseValue.Should().Be("0.16");
        result.Should().ContainKey("Tax.SecondRate").WhoseValue.Should().Be("0.05");
    }

    [Fact]
    public async Task GetSettingsByCategoryAsync_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var settings = new List<Setting>
        {
            CreateTestSetting(category: "Tax")
        };
        var (service, _, _) = BuildServiceWithMocks(settings);

        // Act
        var result = await service.GetSettingsByCategoryAsync("Nonexistent");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSettingsByCategoryAsync_EmptyCategory_ReturnsEmpty()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.GetSettingsByCategoryAsync("");

        // Assert
        result.Should().BeEmpty();
    }
}
