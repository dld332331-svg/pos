#nullable enable

using System.Linq.Expressions;
using Xunit;
using Moq;
using FluentAssertions;
using POS.Application.Services.Implementations;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for PrinterManagementService.PrintReceiptAsync.
/// Tests cover: happy path, no printer, no sale, printer failure, exception handling, and related data loading.
/// </summary>
public class PrinterManagementServicePrintReceiptTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static Sale CreateTestSale(Guid saleId, string invoiceNumber = "INV-001")
    {
        return new Sale
        {
            Id = saleId,
            InvoiceNumber = invoiceNumber,
            UserId = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            CreatedAt = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc),
            SubTotal = 100.000m,
            TaxAmount = 16.000m,
            DiscountAmount = 0m,
            TotalAmount = 116.000m,
            Status = SaleStatus.Completed,
            IsPaid = true,
            PaidAt = new DateTime(2026, 7, 19, 10, 5, 0, DateTimeKind.Utc)
        };
    }

    private static Printer CreateReceiptPrinter(Guid printerId)
    {
        return new Printer
        {
            Id = printerId,
            Name = "Receipt Printer 1",
            PrinterType = PrinterType.Thermal,
            Connection = PrinterConnection.Network,
            IpAddress = "192.168.1.100",
            Port = 9100,
            PaperWidth = 80,
            AssignedRole = PrinterRole.Receipt,
            IsActive = true
        };
    }

    private static User CreateTestUser(Guid userId)
    {
        return new User
        {
            Id = userId,
            FullName = "Test Cashier",
            Username = "cashier1"
        };
    }

    private static Table CreateTestTable(Guid tableId)
    {
        return new Table
        {
            Id = tableId,
            Name = "Table 5",
            ArabicName = "طاولة 5"
        };
    }

    private static Customer CreateTestCustomer(Guid customerId)
    {
        return new Customer
        {
            Id = customerId,
            Name = "John Doe"
        };
    }

    private static SaleItem CreateTestSaleItem(Guid saleId, Guid itemId)
    {
        return new SaleItem
        {
            Id = itemId,
            SaleId = saleId,
            ProductId = Guid.NewGuid(),
            ProductName = "Test Product",
            ProductArabicName = "منتج اختبار",
            Quantity = 2,
            UnitPrice = 50.000m,
            TotalPrice = 100.000m,
            LineTotal = 116.000m,
            TaxRate = 16.000m,
            TaxAmount = 16.000m
        };
    }

    private static Setting CreateSetting(string key, string value)
    {
        return new Setting
        {
            Key = key,
            Value = value,
            Category = "Receipt"
        };
    }

    private static Payment CreateTestPayment(Guid saleId, Guid paymentId)
    {
        return new Payment
        {
            Id = paymentId,
            SaleId = saleId,
            Amount = 116.000m,
            PaymentMethod = PaymentMethod.Cash,
            Timestamp = DateTime.UtcNow
        };
    }

    private static SaleItemModifier CreateTestModifier(Guid saleItemId, string name = "Extra Cheese")
    {
        return new SaleItemModifier
        {
            Id = Guid.NewGuid(),
            SaleItemId = saleItemId,
            ModifierName = name,
            Price = 2.000m
        };
    }

    // ========================================================================
    // Mock Builder
    // ========================================================================

    /// <summary>
    /// Builds a PrinterManagementService with fully mocked dependencies.
    /// Each repository returns the provided data; null means empty/no results.
    /// </summary>
    private (
        PrinterManagementService service,
        Mock<IPrinterService> printerServiceMock,
        Mock<IAuditService> auditServiceMock)
        BuildServiceWithMocks(
            Sale? sale,
            Printer? receiptPrinter,
            List<SaleItem>? saleItems = null,
            List<Payment>? payments = null,
            List<Setting>? settings = null,
            User? user = null,
            Table? table = null,
            Customer? customer = null,
            List<SaleItemModifier>? modifiers = null)
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var printerServiceMock = new Mock<IPrinterService>();
        var auditServiceMock = new Mock<IAuditService>();

        // Audit service — fire-and-forget, always succeeds
        auditServiceMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var saleId = sale?.Id ?? Guid.Empty;

        // ---- Printers repository ----
        var printerRepoMock = new Mock<IRepository<Printer>>();
        printerRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Printer, bool>>>()))
            .ReturnsAsync(receiptPrinter != null
                ? new List<Printer> { receiptPrinter }
                : new List<Printer>());
        unitOfWorkMock.Setup(u => u.Printers).Returns(printerRepoMock.Object);

        // ---- Sales repository ----
        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock
            .Setup(r => r.GetByIdAsync(saleId))
            .ReturnsAsync(sale);
        unitOfWorkMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        // ---- Users repository ----
        var userRepoMock = new Mock<IRepository<User>>();
        userRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(user != null ? new List<User> { user } : new List<User>());
        unitOfWorkMock.Setup(u => u.Users).Returns(userRepoMock.Object);

        // ---- Tables repository ----
        var tableRepoMock = new Mock<IRepository<Table>>();
        tableRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Table, bool>>>()))
            .ReturnsAsync(table != null ? new List<Table> { table } : new List<Table>());
        unitOfWorkMock.Setup(u => u.Tables).Returns(tableRepoMock.Object);

        // ---- Customers repository ----
        var customerRepoMock = new Mock<IRepository<Customer>>();
        customerRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync(customer != null ? new List<Customer> { customer } : new List<Customer>());
        unitOfWorkMock.Setup(u => u.Customers).Returns(customerRepoMock.Object);

        // ---- SaleItems repository ----
        var saleItemRepoMock = new Mock<IRepository<SaleItem>>();
        saleItemRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<SaleItem, bool>>>()))
            .ReturnsAsync(saleItems ?? new List<SaleItem>());
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(saleItemRepoMock.Object);

        // ---- SaleItemModifiers repository ----
        var modifierRepoMock = new Mock<IRepository<SaleItemModifier>>();
        modifierRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<SaleItemModifier, bool>>>()))
            .ReturnsAsync(modifiers ?? new List<SaleItemModifier>());
        unitOfWorkMock.Setup(u => u.SaleItemModifiers).Returns(modifierRepoMock.Object);

        // ---- Payments repository ----
        var paymentRepoMock = new Mock<IRepository<Payment>>();
        paymentRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Payment, bool>>>()))
            .ReturnsAsync(payments ?? new List<Payment>());
        unitOfWorkMock.Setup(u => u.Payments).Returns(paymentRepoMock.Object);

        // ---- Settings repository ----
        var settingRepoMock = new Mock<IRepository<Setting>>();
        settingRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Setting, bool>>>()))
            .ReturnsAsync(settings ?? new List<Setting>());
        unitOfWorkMock.Setup(u => u.Settings).Returns(settingRepoMock.Object);

        // ---- Hardware printer service ----
        printerServiceMock
            .Setup(p => p.PrintReceiptAsync(
                It.IsAny<Printer>(),
                It.IsAny<Sale>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(true);

        var service = new PrinterManagementService(
            unitOfWorkMock.Object,
            printerServiceMock.Object,
            auditServiceMock.Object);

        return (service, printerServiceMock, auditServiceMock);
    }

    // ========================================================================
    // Tests
    // ========================================================================

    [Fact]
    public async Task PrintReceiptAsync_WithActivePrinterAndSale_ReturnsTrue()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var printer = CreateReceiptPrinter(printerId);
        var sale = CreateTestSale(saleId);
        var user = CreateTestUser(Guid.NewGuid());
        var saleItems = new List<SaleItem> { CreateTestSaleItem(saleId, Guid.NewGuid()) };
        var payments = new List<Payment> { CreateTestPayment(saleId, Guid.NewGuid()) };
        var settings = new List<Setting>
        {
            CreateSetting("StoreName", "My Store"),
            CreateSetting("StoreNameArabic", "متجري"),
            CreateSetting("ReceiptFooter", "Thank you!"),
            CreateSetting("ReceiptFooterArabic", "شكراً!")
        };

        var (service, printerServiceMock, _) = BuildServiceWithMocks(
            sale, printer, saleItems, payments, settings, user);

        // Act
        var result = await service.PrintReceiptAsync(saleId);

        // Assert
        result.Should().BeTrue();
        printerServiceMock.Verify(p => p.PrintReceiptAsync(
            It.Is<Printer>(pr => pr.Id == printerId),
            It.Is<Sale>(s => s.Id == saleId),
            "My Store",
            "متجري",
            "Thank you!",
            "شكراً!"), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_WithoutSettings_ReturnsTrueWithDefaults()
    {
        // Arrange — no settings returned from DB; service should use default values
        var saleId = Guid.NewGuid();
        var printer = CreateReceiptPrinter(Guid.NewGuid());
        var sale = CreateTestSale(saleId);
        var saleItems = new List<SaleItem> { CreateTestSaleItem(saleId, Guid.NewGuid()) };

        var (service, printerServiceMock, _) = BuildServiceWithMocks(
            sale, printer, saleItems);

        // Act
        var result = await service.PrintReceiptAsync(saleId);

        // Assert — defaults: "My Store", "متجري", "Thank you for your purchase!", "شكراً لشرائك!"
        result.Should().BeTrue();
        printerServiceMock.Verify(p => p.PrintReceiptAsync(
            It.IsAny<Printer>(),
            It.IsAny<Sale>(),
            "My Store",
            "متجري",
            "Thank you for your purchase!",
            "شكراً لشرائك!"), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_WhenNoReceiptPrinter_ReturnsFalse()
    {
        // Arrange — no receipt printer in the mock
        var saleId = Guid.NewGuid();
        var sale = CreateTestSale(saleId);

        var (service, _, auditServiceMock) = BuildServiceWithMocks(sale, receiptPrinter: null);

        // Act
        var result = await service.PrintReceiptAsync(saleId);

        // Assert
        result.Should().BeFalse();
        auditServiceMock.Verify(a => a.LogAsync(
            null,
            AuditActionType.PrinterConfigChanged,
            "Printer",
            null,
            null,
            null,
            It.Is<string>(s => s.Contains("No active receipt printer"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PrintReceiptAsync_WhenSaleNotFound_ReturnsFalse()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var printer = CreateReceiptPrinter(Guid.NewGuid());

        var (service, _, auditServiceMock) = BuildServiceWithMocks(sale: null, receiptPrinter: printer);

        // Act
        var result = await service.PrintReceiptAsync(saleId);

        // Assert
        result.Should().BeFalse();
        auditServiceMock.Verify(a => a.LogAsync(
            null,
            AuditActionType.PrinterConfigChanged,
            "Sale",
            saleId,
            null,
            null,
            It.Is<string>(s => s.Contains("Sale not found"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PrintReceiptAsync_WhenPrinterServiceFails_ReturnsFalse()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var printer = CreateReceiptPrinter(Guid.NewGuid());
        var sale = CreateTestSale(saleId);
        var saleItems = new List<SaleItem> { CreateTestSaleItem(saleId, Guid.NewGuid()) };

        var (service, printerServiceMock, auditServiceMock) = BuildServiceWithMocks(
            sale, printer, saleItems);

        // Override — printer service returns failure
        printerServiceMock
            .Setup(p => p.PrintReceiptAsync(
                It.IsAny<Printer>(), It.IsAny<Sale>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        // Act
        var result = await service.PrintReceiptAsync(saleId);

        // Assert
        result.Should().BeFalse();
        auditServiceMock.Verify(a => a.LogAsync(
            null,
            AuditActionType.PrinterConfigChanged,
            "Printer",
            printer.Id,
            null,
            null,
            It.Is<string>(s => s.Contains("failed"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PrintReceiptAsync_WhenExceptionThrown_ReturnsFalse()
    {
        // Arrange — User query throws, triggering the catch block
        var saleId = Guid.NewGuid();
        var printer = CreateReceiptPrinter(Guid.NewGuid());
        var sale = CreateTestSale(saleId);

        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var printerServiceMock = new Mock<IPrinterService>();
        var auditServiceMock = new Mock<IAuditService>();

        auditServiceMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // Stub ALL repositories to avoid NullReferenceException on unmocked repos
        // (even though the exception fires before most are reached)
        var emptyPrinterList = new List<Printer>();
        var emptyUserList = new List<User>();
        var emptyTableList = new List<Table>();
        var emptyCustomerList = new List<Customer>();
        var emptySaleItemList = new List<SaleItem>();
        var emptyModifierList = new List<SaleItemModifier>();
        var emptyPaymentList = new List<Payment>();
        var emptySettingList = new List<Setting>();

        // Printer repo — returns a valid printer
        var printerRepoMock = new Mock<IRepository<Printer>>();
        printerRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Printer, bool>>>()))
            .ReturnsAsync(new List<Printer> { printer });
        unitOfWorkMock.Setup(u => u.Printers).Returns(printerRepoMock.Object);

        // Sale repo — returns sale
        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock
            .Setup(r => r.GetByIdAsync(saleId))
            .ReturnsAsync(sale);
        unitOfWorkMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        // User repo — throws on FindAsync (simulates DB failure)
        var userRepoMock = new Mock<IRepository<User>>();
        userRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));
        unitOfWorkMock.Setup(u => u.Users).Returns(userRepoMock.Object);

        // Stub remaining repos to prevent accidental NullReferenceException
        var emptyPrinterRepo = new Mock<IRepository<Table>>();
        emptyPrinterRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Table, bool>>>())).ReturnsAsync(emptyTableList);
        unitOfWorkMock.Setup(u => u.Tables).Returns(emptyPrinterRepo.Object);

        var emptyCustomerRepo = new Mock<IRepository<Customer>>();
        emptyCustomerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(emptyCustomerList);
        unitOfWorkMock.Setup(u => u.Customers).Returns(emptyCustomerRepo.Object);

        var emptySaleItemRepo = new Mock<IRepository<SaleItem>>();
        emptySaleItemRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SaleItem, bool>>>())).ReturnsAsync(emptySaleItemList);
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(emptySaleItemRepo.Object);

        var emptyModifierRepo = new Mock<IRepository<SaleItemModifier>>();
        emptyModifierRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SaleItemModifier, bool>>>())).ReturnsAsync(emptyModifierList);
        unitOfWorkMock.Setup(u => u.SaleItemModifiers).Returns(emptyModifierRepo.Object);

        var emptyPaymentRepo = new Mock<IRepository<Payment>>();
        emptyPaymentRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Payment, bool>>>())).ReturnsAsync(emptyPaymentList);
        unitOfWorkMock.Setup(u => u.Payments).Returns(emptyPaymentRepo.Object);

        var emptySettingRepo = new Mock<IRepository<Setting>>();
        emptySettingRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Setting, bool>>>())).ReturnsAsync(emptySettingList);
        unitOfWorkMock.Setup(u => u.Settings).Returns(emptySettingRepo.Object);

        var service = new PrinterManagementService(
            unitOfWorkMock.Object,
            printerServiceMock.Object,
            auditServiceMock.Object);

        // Act
        var result = await service.PrintReceiptAsync(saleId);

        // Assert
        result.Should().BeFalse();
        auditServiceMock.Verify(a => a.LogAsync(
            null,
            AuditActionType.PrinterConfigChanged,
            "Printer",
            null,
            null,
            null,
            It.Is<string>(s => s.Contains("error"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PrintReceiptAsync_WithTableAndCustomer_SetsNavigationProperties()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var table = CreateTestTable(Guid.NewGuid());
        var customer = CreateTestCustomer(Guid.NewGuid());
        var user = CreateTestUser(Guid.NewGuid());
        var sale = new Sale
        {
            Id = saleId,
            InvoiceNumber = "INV-002",
            UserId = user.Id,
            TableId = table.Id,
            CustomerId = customer.Id,
            CreatedAt = DateTime.UtcNow,
            SubTotal = 50.000m,
            TaxAmount = 8.000m,
            TotalAmount = 58.000m,
            Status = SaleStatus.Completed,
            IsPaid = true
        };
        var printer = CreateReceiptPrinter(Guid.NewGuid());
        var saleItems = new List<SaleItem> { CreateTestSaleItem(saleId, Guid.NewGuid()) };
        var payments = new List<Payment> { CreateTestPayment(saleId, Guid.NewGuid()) };

        var (service, printerServiceMock, _) = BuildServiceWithMocks(
            sale, printer, saleItems, payments, user: user, table: table, customer: customer);

        // Act
        var result = await service.PrintReceiptAsync(saleId);

        // Assert — verify navigation properties are set with correct values
        result.Should().BeTrue();
        printerServiceMock.Verify(p => p.PrintReceiptAsync(
            It.IsAny<Printer>(),
            It.Is<Sale>(s =>
                s.User != null && s.User.FullName == "Test Cashier" &&
                s.Table != null && s.Table.Name == "Table 5" &&
                s.Customer != null && s.Customer.Name == "John Doe"),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_WithSaleItemsAndModifiers_LoadsItemsAndModifiers()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var printer = CreateReceiptPrinter(Guid.NewGuid());
        var sale = CreateTestSale(saleId);
        var user = CreateTestUser(Guid.NewGuid());
        var saleItem = CreateTestSaleItem(saleId, Guid.NewGuid());
        var modifiers = new List<SaleItemModifier>
        {
            CreateTestModifier(saleItem.Id, "Extra Cheese"),
            CreateTestModifier(saleItem.Id, "Large Size")
        };

        var (service, printerServiceMock, _) = BuildServiceWithMocks(
            sale, printer, new List<SaleItem> { saleItem },
            modifiers: modifiers, user: user);

        // Act
        var result = await service.PrintReceiptAsync(saleId);

        // Assert — verify items and modifiers are loaded and passed to printer
        result.Should().BeTrue();
        printerServiceMock.Verify(p => p.PrintReceiptAsync(
            It.IsAny<Printer>(),
            It.Is<Sale>(s => s.SaleItems.Count == 1 && s.SaleItems.First().Modifiers.Count == 2),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_WithPayments_LoadsPayments()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var printer = CreateReceiptPrinter(Guid.NewGuid());
        var sale = CreateTestSale(saleId);
        var user = CreateTestUser(Guid.NewGuid());
        var payments = new List<Payment>
        {
            CreateTestPayment(saleId, Guid.NewGuid()),
            new()
            {
                Id = Guid.NewGuid(),
                SaleId = saleId,
                Amount = 50.000m,
                PaymentMethod = PaymentMethod.Card,
                Timestamp = DateTime.UtcNow,
                ReferenceNumber = "REF-123"
            }
        };

        var (service, printerServiceMock, _) = BuildServiceWithMocks(
            sale, printer, payments: payments, user: user);

        // Act
        var result = await service.PrintReceiptAsync(saleId);

        // Assert
        result.Should().BeTrue();
        printerServiceMock.Verify(p => p.PrintReceiptAsync(
            It.IsAny<Printer>(),
            It.Is<Sale>(s => s.Payments.Count == 2),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    // ========================================================================
    // PrintKitchenTicketsAsync Tests
    // ========================================================================

    [Fact]
    public async Task PrintKitchenTicketsAsync_WithStationAndPrinter_PrintsTicket()
    {
        // Arrange
        var saleId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var sale = CreateTestSale(saleId);

        var saleItem = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = Guid.NewGuid(),
            ProductName = "Pizza",
            ProductArabicName = "بيتزا",
            KitchenStationId = stationId,
            Quantity = 1,
            UnitPrice = 50.000m,
            TotalPrice = 50.000m,
            LineTotal = 58.000m
        };

        var station = new KitchenStation
        {
            Id = stationId,
            Name = "Main Kitchen",
            ArabicName = "المطبخ الرئيسي",
            PrinterId = printerId,
            IsActive = true
        };

        var kitchenPrinter = new Printer
        {
            Id = printerId,
            Name = "Kitchen Printer 1",
            PrinterType = PrinterType.Thermal,
            Connection = PrinterConnection.Network,
            AssignedRole = PrinterRole.Kitchen,
            IsActive = true
        };

        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var printerServiceMock = new Mock<IPrinterService>();
        var auditServiceMock = new Mock<IAuditService>();

        auditServiceMock
            .Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Sales repo
        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(sale);
        unitOfWorkMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        // Users repo
        var userRepoMock = new Mock<IRepository<User>>();
        userRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User>());
        unitOfWorkMock.Setup(u => u.Users).Returns(userRepoMock.Object);

        // Tables repo
        unitOfWorkMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);

        // SaleItems repo — return the item with KitchenStationId
        var saleItemRepoMock = new Mock<IRepository<SaleItem>>();
        saleItemRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<SaleItem, bool>>>()))
            .ReturnsAsync(new List<SaleItem> { saleItem });
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(saleItemRepoMock.Object);

        // SaleItemModifiers repo — no modifiers
        unitOfWorkMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);

        // KitchenStations repo — return the configured station
        var stationRepoMock = new Mock<IRepository<KitchenStation>>();
        stationRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<KitchenStation> { station });
        unitOfWorkMock.Setup(u => u.KitchenStations).Returns(stationRepoMock.Object);

        // Printers repo — return the kitchen printer
        var printerRepoMock = new Mock<IRepository<Printer>>();
        printerRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Printer, bool>>>()))
            .ReturnsAsync(new List<Printer> { kitchenPrinter });
        unitOfWorkMock.Setup(u => u.Printers).Returns(printerRepoMock.Object);

        // Hardware printer service — succeed
        printerServiceMock
            .Setup(p => p.PrintKitchenTicketAsync(
                It.IsAny<Printer>(), It.IsAny<Sale>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var service = new PrinterManagementService(unitOfWorkMock.Object, printerServiceMock.Object, auditServiceMock.Object);

        // Act
        var result = await service.PrintKitchenTicketsAsync(saleId);

        // Assert
        result.Should().BeTrue();
        printerServiceMock.Verify(p => p.PrintKitchenTicketAsync(
            It.Is<Printer>(pr => pr.Id == printerId),
            It.Is<Sale>(s => s.Id == saleId),
            It.Is<string>(name => name == "Main Kitchen")), Times.Once);
    }

    [Fact]
    public async Task PrintKitchenTicketsAsync_SaleNotFound_ReturnsFalse()
    {
        // Arrange
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var printerServiceMock = new Mock<IPrinterService>();
        var auditServiceMock = new Mock<IAuditService>();

        auditServiceMock
            .Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Sale?)null);
        unitOfWorkMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var service = new PrinterManagementService(unitOfWorkMock.Object, printerServiceMock.Object, auditServiceMock.Object);

        // Act
        var result = await service.PrintKitchenTicketsAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    private static Mock<IRepository<T>> CreateEmptyRepoMock<T>() where T : BaseEntity
    {
        var mock = new Mock<IRepository<T>>();
        mock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<T, bool>>>()))
            .ReturnsAsync(new List<T>());
        return mock;
    }
}
