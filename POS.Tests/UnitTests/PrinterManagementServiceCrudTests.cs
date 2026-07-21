#nullable enable

using System.Linq.Expressions;
using Xunit;
using Moq;
using FluentAssertions;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Application.Services.Implementations;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for PrinterManagementService CRUD and other non-receipt methods.
///
/// Test areas:
///   1. GetPrintersAsync — all printers mapped to DTOs
///   2. AddPrinterAsync — valid enum parsing, invalid enum values
///   3. UpdatePrinterAsync — field updates, not found, null DTO
///   4. DeletePrinterAsync — soft delete, not found
///   5. TestPrinterAsync — delegates to IPrinterService, printer not found
///   6. GetKitchenStationsAsync — stations with printer name lookup
///   7. AddKitchenStationAsync — creates station with ArabicName
///   8. OpenCashDrawerAsync — delegates to printer service, no printer
/// </summary>
public class PrinterManagementServiceCrudTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static readonly Guid DefaultPrinterId = Guid.NewGuid();
    private static readonly Guid DefaultStationId = Guid.NewGuid();

    private static Printer CreatePrinter(
        Guid? id = null,
        string name = "طابعة الفواتير",
        PrinterType printerType = PrinterType.Thermal,
        PrinterConnection connection = PrinterConnection.Network,
        string? ipAddress = "192.168.1.100",
        int port = 9100,
        int paperWidth = 80,
        PrinterRole role = PrinterRole.Receipt,
        bool isActive = true)
    {
        return new Printer
        {
            Id = id ?? DefaultPrinterId,
            Name = name,
            PrinterType = printerType,
            Connection = connection,
            IpAddress = ipAddress,
            Port = port,
            PaperWidth = paperWidth,
            AssignedRole = role,
            IsActive = isActive
        };
    }

    private static KitchenStation CreateStation(
        Guid? id = null,
        string name = "مطبخ رئيسي",
        Guid? printerId = null,
        bool isActive = true)
    {
        return new KitchenStation
        {
            Id = id ?? DefaultStationId,
            Name = name,
            PrinterId = printerId,
            IsActive = isActive
        };
    }

    private static PrinterDto CreatePrinterDto(
        Guid? id = null,
        string name = "طابعة معدلة",
        string printerType = "Thermal",
        string connection = "USB",
        string? ipAddress = "192.168.1.200",
        string? port = "9101",
        int paperWidth = 80,
        string role = "Kitchen",
        bool isActive = false)
    {
        return new PrinterDto(
            id ?? DefaultPrinterId,
            name,
            printerType,
            connection,
            ipAddress,
            port,
            paperWidth,
            role,
            isActive);
    }

    // ========================================================================
    // Mock Builder
    // ========================================================================

    /// <summary>
    /// Builds a PrinterManagementService with fully mocked dependencies.
    /// </summary>
    private (PrinterManagementService service,
             Mock<IUnitOfWork> unitOfWorkMock,
             Mock<IPrinterService> printerServiceMock,
             Mock<IAuditService> auditMock)
        BuildServiceWithMocks(
            Printer? singlePrinter = null,
            List<Printer>? allPrinters = null,
            List<KitchenStation>? stations = null)
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var printerServiceMock = new Mock<IPrinterService>();
        var auditMock = new Mock<IAuditService>();

        // ---- Audit (fire-and-forget) ----
        auditMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // ---- SaveChanges ----
        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // ---- Printers ----
        var printerRepoMock = new Mock<IRepository<Printer>>();
        printerRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(allPrinters ?? new List<Printer>());
        printerRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(singlePrinter);
        printerRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Printer>()))
            .Returns(Task.CompletedTask);
        printerRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Printer>()))
            .Returns(Task.CompletedTask);
        printerRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Printer, bool>>>()))
            .ReturnsAsync(singlePrinter is not null
                ? new List<Printer> { singlePrinter }
                : new List<Printer>());
        unitOfWorkMock.Setup(u => u.Printers).Returns(printerRepoMock.Object);

        // ---- KitchenStations ----
        var stationRepoMock = new Mock<IRepository<KitchenStation>>();
        stationRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(stations ?? new List<KitchenStation>());
        stationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<KitchenStation>()))
            .Returns(Task.CompletedTask);
        stationRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<KitchenStation, bool>>>()))
            .ReturnsAsync(new List<KitchenStation>());
        unitOfWorkMock.Setup(u => u.KitchenStations).Returns(stationRepoMock.Object);

        // ---- Sales (for OpenCashDrawerAsync) ----
        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Sale?)null);
        unitOfWorkMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        // ---- Stub remaining repos to prevent NullReferenceException ----
        var emptyUserRepo = new Mock<IRepository<User>>();
        emptyUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(new List<User>());
        unitOfWorkMock.Setup(u => u.Users).Returns(emptyUserRepo.Object);

        var emptyTableRepo = new Mock<IRepository<Table>>();
        emptyTableRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Table, bool>>>())).ReturnsAsync(new List<Table>());
        unitOfWorkMock.Setup(u => u.Tables).Returns(emptyTableRepo.Object);

        var emptyCustomerRepo = new Mock<IRepository<Customer>>();
        emptyCustomerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Customer, bool>>>())).ReturnsAsync(new List<Customer>());
        unitOfWorkMock.Setup(u => u.Customers).Returns(emptyCustomerRepo.Object);

        var emptySaleItemRepo = new Mock<IRepository<SaleItem>>();
        emptySaleItemRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SaleItem, bool>>>())).ReturnsAsync(new List<SaleItem>());
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(emptySaleItemRepo.Object);

        var emptyModifierRepo = new Mock<IRepository<SaleItemModifier>>();
        emptyModifierRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SaleItemModifier, bool>>>())).ReturnsAsync(new List<SaleItemModifier>());
        unitOfWorkMock.Setup(u => u.SaleItemModifiers).Returns(emptyModifierRepo.Object);

        var emptyPaymentRepo = new Mock<IRepository<Payment>>();
        emptyPaymentRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Payment, bool>>>())).ReturnsAsync(new List<Payment>());
        unitOfWorkMock.Setup(u => u.Payments).Returns(emptyPaymentRepo.Object);

        var emptySettingRepo = new Mock<IRepository<Setting>>();
        emptySettingRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Setting, bool>>>())).ReturnsAsync(new List<Setting>());
        unitOfWorkMock.Setup(u => u.Settings).Returns(emptySettingRepo.Object);

        var emptyProductRepo = new Mock<IRepository<Product>>();
        emptyProductRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Product, bool>>>())).ReturnsAsync(new List<Product>());
        unitOfWorkMock.Setup(u => u.Products).Returns(emptyProductRepo.Object);

        var service = new PrinterManagementService(
            unitOfWorkMock.Object,
            printerServiceMock.Object,
            auditMock.Object);

        return (service, unitOfWorkMock, printerServiceMock, auditMock);
    }

    // ========================================================================
    // GetPrintersAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetPrintersAsync_ReturnsAllPrintersMappedToDto()
    {
        // Arrange
        var printers = new List<Printer>
        {
            CreatePrinter(id: Guid.NewGuid(), name: "طابعة 1", role: PrinterRole.Receipt),
            CreatePrinter(id: Guid.NewGuid(), name: "طابعة 2", role: PrinterRole.Kitchen, isActive: false)
        };

        var (service, _, _, _) = BuildServiceWithMocks(allPrinters: printers);

        // Act
        var result = await service.GetPrintersAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("طابعة 1");
        result[0].PrinterType.Should().Be("Thermal");
        result[0].Connection.Should().Be("Network");
        result[0].IpAddress.Should().Be("192.168.1.100");
        result[0].Port.Should().Be("9100");
        result[0].PaperWidth.Should().Be(80);
        result[0].AssignedRole.Should().Be("Receipt");
        result[0].IsActive.Should().BeTrue();

        result[1].Name.Should().Be("طابعة 2");
        result[1].AssignedRole.Should().Be("Kitchen");
        result[1].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetPrintersAsync_NoPrinters_ReturnsEmpty()
    {
        var (service, _, _, _) = BuildServiceWithMocks(allPrinters: new List<Printer>());
        var result = await service.GetPrintersAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPrintersAsync_PrinterWithDefaultPort_ReturnsNullPort()
    {
        // Arrange — port 0 should be returned as null in the DTO
        var printer = CreatePrinter(port: 0);
        var (service, _, _, _) = BuildServiceWithMocks(allPrinters: new List<Printer> { printer });

        var result = await service.GetPrintersAsync();

        result.Should().HaveCount(1);
        result[0].Port.Should().BeNull();
    }

    // ========================================================================
    // AddPrinterAsync Tests
    // ========================================================================

    [Fact]
    public async Task AddPrinterAsync_ValidRequest_ReturnsCreatedPrinter()
    {
        // Arrange
        var (service, unitOfWorkMock, _, auditMock) = BuildServiceWithMocks();

        // Act
        var result = await service.AddPrinterAsync(
            "طابعة جديدة", "Thermal", "USB", null, null, 80, "Receipt");

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("طابعة جديدة");
        result.PrinterType.Should().Be("Thermal");
        result.Connection.Should().Be("USB");
        result.PaperWidth.Should().Be(80);
        result.AssignedRole.Should().Be("Receipt");
        result.IsActive.Should().BeTrue();

        unitOfWorkMock.Verify(u => u.Printers.AddAsync(
            It.Is<Printer>(p =>
                p.Name == "طابعة جديدة" &&
                p.PrinterType == PrinterType.Thermal &&
                p.Connection == PrinterConnection.USB &&
                p.AssignedRole == PrinterRole.Receipt &&
                p.IsActive)), Times.Once);

        auditMock.Verify(a => a.LogAsync(
            null, AuditActionType.PrinterConfigChanged, "Printer",
            It.IsAny<Guid>(), null,
            It.Is<string>(s => s.Contains("طابعة جديدة")),
            null), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AddPrinterAsync_InvalidPrinterType_ThrowsInvalidOperationException()
    {
        var (service, _, _, _) = BuildServiceWithMocks();

        var act = () => service.AddPrinterAsync(
            "Test", "InvalidType", "USB", null, null, 80, "Receipt");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("نوع الطابعة غير صالح");
    }

    [Fact]
    public async Task AddPrinterAsync_InvalidConnection_ThrowsInvalidOperationException()
    {
        var (service, _, _, _) = BuildServiceWithMocks();

        var act = () => service.AddPrinterAsync(
            "Test", "Thermal", "InvalidConnection", null, null, 80, "Receipt");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("نوع الاتصال غير صالح");
    }

    [Fact]
    public async Task AddPrinterAsync_InvalidRole_ThrowsInvalidOperationException()
    {
        var (service, _, _, _) = BuildServiceWithMocks();

        var act = () => service.AddPrinterAsync(
            "Test", "Thermal", "USB", null, null, 80, "InvalidRole");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("دور الطابعة غير صالح");
    }

    [Fact]
    public async Task AddPrinterAsync_ZeroPaperWidth_DefaultsTo80()
    {
        var (service, unitOfWorkMock, _, _) = BuildServiceWithMocks();

        await service.AddPrinterAsync(
            "Test", "Thermal", "USB", null, null, 0, "Receipt");

        unitOfWorkMock.Verify(u => u.Printers.AddAsync(
            It.Is<Printer>(p => p.PaperWidth == 80)), Times.Once);
    }

    [Fact]
    public async Task AddPrinterAsync_NetworkWithPort_FallsBackToDefault9100()
    {
        var (service, unitOfWorkMock, _, _) = BuildServiceWithMocks();

        await service.AddPrinterAsync(
            "Test", "Thermal", "Network", "192.168.1.50", "invalid_port", 80, "Receipt");

        unitOfWorkMock.Verify(u => u.Printers.AddAsync(
            It.Is<Printer>(p => p.Port == 9100)), Times.Once);
    }

    // ========================================================================
    // UpdatePrinterAsync Tests
    // ========================================================================

    [Fact]
    public async Task UpdatePrinterAsync_ValidRequest_UpdatesAllFields()
    {
        // Arrange
        var existing = CreatePrinter(
            name: "الاسم القديم",
            printerType: PrinterType.Thermal,
            connection: PrinterConnection.Network,
            ipAddress: "192.168.1.100",
            port: 9100,
            paperWidth: 80,
            role: PrinterRole.Receipt,
            isActive: true);

        var updateDto = CreatePrinterDto(
            id: DefaultPrinterId,
            name: "الاسم الجديد",
            printerType: "DotMatrix",
            connection: "USB",
            ipAddress: "192.168.1.200",
            port: "9101",
            paperWidth: 58,
            role: "Kitchen",
            isActive: false);

        var (service, unitOfWorkMock, _, auditMock) = BuildServiceWithMocks(singlePrinter: existing);

        // Act
        var result = await service.UpdatePrinterAsync(updateDto);

        // Assert
        result.Success.Should().BeTrue();
        result.SuccessMessage.Should().Be("تم تحديث الطابعة بنجاح");

        existing.Name.Should().Be("الاسم الجديد");
        existing.PrinterType.Should().Be(PrinterType.DotMatrix);
        existing.Connection.Should().Be(PrinterConnection.USB);
        existing.IpAddress.Should().Be("192.168.1.200");
        existing.Port.Should().Be(9101);
        existing.PaperWidth.Should().Be(58);
        existing.AssignedRole.Should().Be(PrinterRole.Kitchen);
        existing.IsActive.Should().BeFalse();

        unitOfWorkMock.Verify(u => u.Printers.UpdateAsync(
            It.Is<Printer>(p => p.Name == "الاسم الجديد")), Times.Once);

        // Audit logged with before and after values
        auditMock.Verify(a => a.LogAsync(
            null, AuditActionType.PrinterConfigChanged, "Printer",
            DefaultPrinterId,
            It.Is<string>(b => b.Contains("الاسم القديم") && b.Contains("Active=True")),
            It.Is<string>(a => a.Contains("الاسم الجديد") && a.Contains("Active=False")),
            null), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdatePrinterAsync_PrinterNotFound_ReturnsFailure()
    {
        var updateDto = CreatePrinterDto();
        var (service, _, _, _) = BuildServiceWithMocks(singlePrinter: null);

        var result = await service.UpdatePrinterAsync(updateDto);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("الطابعة غير موجودة");
    }

    [Fact]
    public async Task UpdatePrinterAsync_NullDto_ThrowsArgumentNullException()
    {
        var (service, _, _, _) = BuildServiceWithMocks();

        var act = () => service.UpdatePrinterAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdatePrinterAsync_InvalidEnumValues_KeepsExisting()
    {
        // Arrange — invalid enum strings should be ignored (existing values preserved)
        var existing = CreatePrinter(
            printerType: PrinterType.Thermal,
            connection: PrinterConnection.Network,
            role: PrinterRole.Receipt);

        var updateDto = new PrinterDto(
            DefaultPrinterId, "محدث", "BadType", "BadConn", null, null, 80, "BadRole", true);

        var (service, _, _, _) = BuildServiceWithMocks(singlePrinter: existing);

        // Act
        var result = await service.UpdatePrinterAsync(updateDto);

        // Assert — invalid enums leave existing values unchanged
        result.Success.Should().BeTrue();
        existing.Name.Should().Be("محدث"); // Name always updates
        existing.PrinterType.Should().Be(PrinterType.Thermal); // unchanged (parse failed)
        existing.Connection.Should().Be(PrinterConnection.Network); // unchanged
        existing.AssignedRole.Should().Be(PrinterRole.Receipt); // unchanged
    }

    [Fact]
    public async Task UpdatePrinterAsync_NullPort_LeavesPortUnchanged()
    {
        // Arrange
        var existing = CreatePrinter(port: 9100);
        var updateDto = new PrinterDto(
            DefaultPrinterId, "محدث", "Thermal", "USB", null, null, 80, "Receipt", true);

        var (service, _, _, _) = BuildServiceWithMocks(singlePrinter: existing);

        var result = await service.UpdatePrinterAsync(updateDto);

        result.Success.Should().BeTrue();
        existing.Port.Should().Be(9100); // unchanged (null port means no parse)
    }

    // ========================================================================
    // DeletePrinterAsync Tests
    // ========================================================================

    [Fact]
    public async Task DeletePrinterAsync_ExistingPrinter_MarksAsDeleted()
    {
        // Arrange
        var printer = CreatePrinter(name: "طابعة للحذف");
        var (service, unitOfWorkMock, _, auditMock) = BuildServiceWithMocks(singlePrinter: printer);

        // Act
        var result = await service.DeletePrinterAsync(DefaultPrinterId);

        // Assert
        result.Success.Should().BeTrue();
        result.SuccessMessage.Should().Be("تم حذف الطابعة بنجاح");

        // MarkAsDeleted was called (sets IsDeleted flag)
        unitOfWorkMock.Verify(u => u.Printers.UpdateAsync(
            It.Is<Printer>(p => p.Id == DefaultPrinterId)), Times.Once);

        // Audit logged
        auditMock.Verify(a => a.LogAsync(
            null, AuditActionType.PrinterConfigChanged, "Printer",
            DefaultPrinterId,
            It.Is<string>(b => b.Contains("طابعة للحذف")),
            null,
            "Printer deleted"), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeletePrinterAsync_PrinterNotFound_ReturnsFailure()
    {
        var (service, _, _, _) = BuildServiceWithMocks(singlePrinter: null);

        var result = await service.DeletePrinterAsync(Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("الطابعة غير موجودة");
    }

    // ========================================================================
    // TestPrinterAsync Tests
    // ========================================================================

    [Fact]
    public async Task TestPrinterAsync_ExistingPrinter_DelegatesToPrinterService()
    {
        var printer = CreatePrinter();
        var (service, _, printerServiceMock, _) = BuildServiceWithMocks(singlePrinter: printer);

        printerServiceMock
            .Setup(p => p.TestPrinterAsync(printer))
            .ReturnsAsync(true);

        var result = await service.TestPrinterAsync(DefaultPrinterId);

        result.Should().BeTrue();
        printerServiceMock.Verify(p => p.TestPrinterAsync(printer), Times.Once);
    }

    [Fact]
    public async Task TestPrinterAsync_PrinterNotFound_ReturnsFalse()
    {
        var (service, _, _, _) = BuildServiceWithMocks(singlePrinter: null);

        var result = await service.TestPrinterAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TestPrinterAsync_PrinterServiceFails_ReturnsFalse()
    {
        var printer = CreatePrinter();
        var (service, _, printerServiceMock, _) = BuildServiceWithMocks(singlePrinter: printer);

        printerServiceMock
            .Setup(p => p.TestPrinterAsync(printer))
            .ReturnsAsync(false);

        var result = await service.TestPrinterAsync(DefaultPrinterId);

        result.Should().BeFalse();
    }

    // ========================================================================
    // GetKitchenStationsAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetKitchenStationsAsync_ReturnsStationsWithPrinterNames()
    {
        var printer = CreatePrinter(id: DefaultPrinterId, name: "طابعة المطبخ");
        var station = CreateStation(id: DefaultStationId, name: "مطبخ اللحوم", printerId: DefaultPrinterId);
        var station2 = CreateStation(id: Guid.NewGuid(), name: "مطبخ بلا طابعة", printerId: null);

        var (service, _, _, _) = BuildServiceWithMocks(
            singlePrinter: printer,
            allPrinters: new List<Printer> { printer },
            stations: new List<KitchenStation> { station, station2 });

        var result = await service.GetKitchenStationsAsync();

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("مطبخ اللحوم");
        result[0].PrinterId.Should().Be(DefaultPrinterId);
        result[0].PrinterName.Should().Be("طابعة المطبخ");
        result[0].IsActive.Should().BeTrue();

        result[1].Name.Should().Be("مطبخ بلا طابعة");
        result[1].PrinterId.Should().BeNull();
        result[1].PrinterName.Should().BeNull();
    }

    [Fact]
    public async Task GetKitchenStationsAsync_NoStations_ReturnsEmpty()
    {
        var (service, _, _, _) = BuildServiceWithMocks(stations: new List<KitchenStation>());
        var result = await service.GetKitchenStationsAsync();
        result.Should().BeEmpty();
    }

    // ========================================================================
    // AddKitchenStationAsync Tests
    // ========================================================================

    [Fact]
    public async Task AddKitchenStationAsync_WithPrinter_ReturnsCreatedStation()
    {
        var printer = CreatePrinter(name: "طابعة المطبخ");
        var (service, unitOfWorkMock, _, _) = BuildServiceWithMocks(
            singlePrinter: printer,
            allPrinters: new List<Printer> { printer });

        var result = await service.AddKitchenStationAsync("مطبخ البيتزا", DefaultPrinterId);

        result.Should().NotBeNull();
        result.Name.Should().Be("مطبخ البيتزا");
        result.PrinterId.Should().Be(DefaultPrinterId);
        result.PrinterName.Should().Be("طابعة المطبخ");
        result.IsActive.Should().BeTrue();

        unitOfWorkMock.Verify(u => u.KitchenStations.AddAsync(
            It.Is<KitchenStation>(s =>
                s.Name == "مطبخ البيتزا" &&
                s.PrinterId == DefaultPrinterId &&
                s.IsActive)), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AddKitchenStationAsync_WithoutPrinter_ReturnsStationWithNullPrinterName()
    {
        var (service, _, _, _) = BuildServiceWithMocks();

        var result = await service.AddKitchenStationAsync("مطبخ رئيسي", null);

        result.Name.Should().Be("مطبخ رئيسي");
        result.PrinterId.Should().BeNull();
        result.PrinterName.Should().BeNull();
        result.IsActive.Should().BeTrue();
    }

    // ========================================================================
    // OpenCashDrawerAsync Tests
    // ========================================================================

    [Fact]
    public async Task OpenCashDrawerAsync_ExistingPrinter_DelegatesAndReturnsTrue()
    {
        var printer = CreatePrinter(name: "طابعة الدرج");
        var (service, _, printerServiceMock, auditMock) = BuildServiceWithMocks(singlePrinter: printer);

        printerServiceMock
            .Setup(p => p.OpenCashDrawerAsync(printer))
            .ReturnsAsync(true);

        var result = await service.OpenCashDrawerAsync();

        result.Should().BeTrue();
        printerServiceMock.Verify(p => p.OpenCashDrawerAsync(printer), Times.Once);

        auditMock.Verify(a => a.LogAsync(
            null, AuditActionType.PrinterConfigChanged, "Printer",
            DefaultPrinterId, null, null,
            It.Is<string>(s => s.Contains("Cash drawer opened"))), Times.Once);
    }

    [Fact]
    public async Task OpenCashDrawerAsync_NoReceiptPrinter_ReturnsFalse()
    {
        var (service, _, _, auditMock) = BuildServiceWithMocks(singlePrinter: null);

        var result = await service.OpenCashDrawerAsync();

        result.Should().BeFalse();

        auditMock.Verify(a => a.LogAsync(
            null, AuditActionType.PrinterConfigChanged, "Printer",
            null, null, null,
            It.Is<string>(s => s.Contains("No active receipt printer"))), Times.Once);
    }

    [Fact]
    public async Task OpenCashDrawerAsync_PrinterServiceFails_ReturnsFalse()
    {
        var printer = CreatePrinter();
        var (service, _, printerServiceMock, auditMock) = BuildServiceWithMocks(singlePrinter: printer);

        printerServiceMock
            .Setup(p => p.OpenCashDrawerAsync(printer))
            .ReturnsAsync(false);

        var result = await service.OpenCashDrawerAsync();

        result.Should().BeFalse();

        // Audit logged the failure
        auditMock.Verify(a => a.LogAsync(
            null, AuditActionType.PrinterConfigChanged, "Printer",
            DefaultPrinterId, null, null,
            It.Is<string>(s => s.Contains("failed"))), Times.Once);
    }
}
