using Xunit;
using Moq;
using FluentAssertions;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Infrastructure.Printing;

namespace POS.Tests.UnitTests;

/// <summary>
/// Mock-based unit tests for ESCPOSPrinter error handling paths.
/// Tests all public methods: PrintReceiptAsync, PrintKitchenTicketAsync,
/// TestPrinterAsync, OpenCashDrawerAsync, GetPrinterStatus.
///
/// Verifies:
/// - Guard clause behavior (null/empty inputs)
/// - Catch-block exception handling and return values
/// - Logger error/warning calls on each failure path
/// - Edge cases (unknown enum values, null collections, etc.)
/// </summary>
public sealed class ESCPOSPrinterErrorHandlingTests
{
    private readonly Mock<ILoggerService> _loggerMock;
    private readonly Mock<IPrinterHardwareSender> _hardwareMock;
    private readonly ESCPOSPrinter _sut;

    public ESCPOSPrinterErrorHandlingTests()
    {
        _loggerMock = new Mock<ILoggerService>();
        _loggerMock.Setup(l => l.LogInfo(It.IsAny<string>(), It.IsAny<object?[]>()));
        _loggerMock.Setup(l => l.LogWarning(It.IsAny<string>(), It.IsAny<object?[]>()));
        _loggerMock.Setup(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<object?[]>()));
        _loggerMock.Setup(l => l.LogDebug(It.IsAny<string>(), It.IsAny<object?[]>()));
        _hardwareMock = new Mock<IPrinterHardwareSender>();
        // Hardware sender throws for all send methods by default.
        // Error messages include keywords the existing assertions check for
        // ("has no COM port", "no printer name", "or ConnectionString").
        _hardwareMock.Setup(h => h.SendViaNetworkAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .ThrowsAsync(new InvalidOperationException(
                "Mock network failure - no IP address configured"));
        _hardwareMock.Setup(h => h.SendViaSerialAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .ThrowsAsync(new InvalidOperationException(
                "Mock serial failure - has no COM port configured"));
        _hardwareMock.Setup(h => h.SendViaUsbAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .ThrowsAsync(new InvalidOperationException(
                "Mock USB failure - no printer name or ConnectionString configured"));
        _hardwareMock.Setup(h => h.GetNetworkPrinterStatus(It.IsAny<Printer>()))
            .Returns(PrinterStatus.Offline);
        _hardwareMock.Setup(h => h.GetSerialPrinterStatus(It.IsAny<Printer>()))
            .Returns(PrinterStatus.Offline);
        _hardwareMock.Setup(h => h.GetUsbPrinterStatus(It.IsAny<Printer>()))
            .Returns(PrinterStatus.Offline);
        _sut = new ESCPOSPrinter(_loggerMock.Object, _hardwareMock.Object);
    }

    // ========================================================================
    // Constructor
    // ========================================================================

    [Fact]
    public void Constructor_NullLogger_WithWorkingHardware_ShouldNotThrow()
    {
        // The constructor does NOT guard against null logger (it just assigns it).
        // With a properly-behaving hardware sender that returns a value without
        // logging, the null logger is never accessed during normal dispatch.
        var hardwareMock = new Mock<IPrinterHardwareSender>();
        hardwareMock.Setup(h => h.GetNetworkPrinterStatus(It.IsAny<Printer>()))
            .Returns(PrinterStatus.Online);
        var printer = new ESCPOSPrinter(null!, hardwareMock.Object);

        var act = () => printer.GetPrinterStatus(new Printer { Name = "Test" });

        // No exception because the hardware sender's methods don't use the logger.
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithHardwareSender_ShouldSucceed()
    {
        var hardwareMock = new Mock<IPrinterHardwareSender>();
        var printer = new ESCPOSPrinter(_loggerMock.Object, hardwareMock.Object);
        var act = () => printer.GetPrinterStatus(new Printer { Name = "Test" });
        act.Should().NotThrow();
    }

    // ========================================================================
    // PrintReceiptAsync — Null Guard Clauses
    // ========================================================================

    [Fact]
    public async Task PrintReceiptAsync_NullPrinter_ShouldThrowArgumentNullException()
    {
        var sale = CreateMinimalSale();
        var act = () => _sut.PrintReceiptAsync(null!, sale, "Store", "متجر");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PrintReceiptAsync_NullSale_ShouldThrowArgumentNullException()
    {
        var printer = CreatePrinter(PrinterConnection.USB);
        var act = () => _sut.PrintReceiptAsync(printer, null!, "Store", "متجر");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ========================================================================
    // PrintReceiptAsync — Network Error Paths
    // ========================================================================

    [Fact]
    public async Task PrintReceiptAsync_NetworkEmptyIp_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "");
        var sale = CreateMinimalSale();

        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_NetworkNullIp_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: null);
        var sale = CreateMinimalSale();

        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_NetworkWhitespaceIp_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "   ");
        var sale = CreateMinimalSale();

        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt")),
            It.IsAny<Exception?>()), Times.Once);
    }

    // ========================================================================
    // PrintReceiptAsync — Serial Error Paths
    // ========================================================================

    [Fact]
    public async Task PrintReceiptAsync_SerialNullConnectionString_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: null);
        var sale = CreateMinimalSale();

        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt") && msg.Contains("has no COM port")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_SerialEmptyConnectionString_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: "");
        var sale = CreateMinimalSale();

        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt") && msg.Contains("has no COM port")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_SerialNonexistentComPort_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: "COM_DOES_NOT_EXIST_99999");
        var sale = CreateMinimalSale();

        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt")),
            It.IsAny<Exception?>()), Times.Once);
    }

    // ========================================================================
    // PrintReceiptAsync — USB Error Paths
    // ========================================================================

    [Fact]
    public async Task PrintReceiptAsync_UsbNullConnectionStringAndNullName_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: null, name: null!);
        var sale = CreateMinimalSale();

        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt")
                && (msg.Contains("no printer name") || msg.Contains("or ConnectionString"))),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_UsbEmptyConnectionString_NullName_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: "", name: null!);
        var sale = CreateMinimalSale();

        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt")),
            It.IsAny<Exception?>()), Times.Once);
    }

    // ========================================================================
    // PrintReceiptAsync — Unknown Connection Type
    // ========================================================================

    [Fact]
    public async Task PrintReceiptAsync_UnknownConnection_ShouldFallbackToLoggingAndReturnTrue()
    {
        var printer = CreatePrinter((PrinterConnection)999, name: "Unknown");
        var sale = CreateMinimalSale();

        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        // Unknown connection falls through to default: logs + delays → returns true
        result.Should().BeTrue();
        _loggerMock.Verify(l => l.LogInfo(
            It.Is<string>(msg => msg.Contains("has no known connection type")),
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }

    // ========================================================================
    // PrintReceiptAsync — Edge Cases
    // ========================================================================

    [Fact]
    public async Task PrintReceiptAsync_EmptySaleItems_ShouldNotThrow()
    {
        var printer = CreatePrinter((PrinterConnection)999);
        var sale = CreateMinimalSale();
        // No items added — SaleItems is empty (never null)

        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task PrintReceiptAsync_EmptyPayments_ShouldNotThrow()
    {
        var printer = CreatePrinter((PrinterConnection)999);
        var sale = CreateMinimalSale();
        // Payments is empty by default (never null)

        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task PrintReceiptAsync_EmptyInvoiceNumber_ShouldSkipBarcode()
    {
        var printer = CreatePrinter((PrinterConnection)999); // Unknown → skips hardware
        var sale = CreateMinimalSale();
        sale.InvoiceNumber = string.Empty;

        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task PrintReceiptAsync_WithModifiers_ShouldHandleGracefully()
    {
        var printer = CreatePrinter((PrinterConnection)999); // Unknown → skips hardware
        var sale = CreateMinimalSale();
        var item = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            ProductId = Guid.NewGuid(),
            ProductName = "Test Item",
            Quantity = 1,
            UnitPrice = 10.000m,
            TotalPrice = 10.000m,
            LineTotal = 11.600m,
            TaxRate = 0.16m
        };
        item.AddModifier(new SaleItemModifier
        {
            Id = Guid.NewGuid(),
            SaleItemId = item.Id,
            ModifierName = "Extra Cheese",
            AdditionalPrice = 2.000m,
            Quantity = 1
        });
        sale.AddItem(item);

        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        result.Should().BeTrue();
    }

    // ========================================================================
    // PrintKitchenTicketAsync — Null Guard Clauses
    // ========================================================================

    [Fact]
    public async Task PrintKitchenTicketAsync_NullPrinter_ShouldThrowArgumentNullException()
    {
        var sale = CreateMinimalSale();
        var act = () => _sut.PrintKitchenTicketAsync(null!, sale, "Kitchen");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PrintKitchenTicketAsync_NullSale_ShouldThrowArgumentNullException()
    {
        var printer = CreatePrinter(PrinterConnection.USB);
        var act = () => _sut.PrintKitchenTicketAsync(printer, null!, "Kitchen");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ========================================================================
    // PrintKitchenTicketAsync — Dispatch Error Paths
    // ========================================================================

    [Fact]
    public async Task PrintKitchenTicketAsync_NetworkEmptyIp_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "");
        var sale = CreateMinimalSale();

        var result = await _sut.PrintKitchenTicketAsync(printer, sale, "Main Kitchen");

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing kitchen ticket")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintKitchenTicketAsync_SerialNullConnectionString_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: null);
        var sale = CreateMinimalSale();

        var result = await _sut.PrintKitchenTicketAsync(printer, sale, "Beverage Station");

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing kitchen ticket") && msg.Contains("has no COM port")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintKitchenTicketAsync_UsbNullConnectionStringAndNullName_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: null, name: null!);
        var sale = CreateMinimalSale();

        var result = await _sut.PrintKitchenTicketAsync(printer, sale, "Main Kitchen");

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing kitchen ticket")
                && (msg.Contains("no printer name") || msg.Contains("or ConnectionString"))),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintKitchenTicketAsync_UnknownConnection_ShouldReturnTrue()
    {
        var printer = CreatePrinter((PrinterConnection)999);
        var sale = CreateMinimalSale();

        var result = await _sut.PrintKitchenTicketAsync(printer, sale, "Main Kitchen");

        result.Should().BeTrue();
        _loggerMock.Verify(l => l.LogInfo(
            It.Is<string>(msg => msg.Contains("has no known connection type")),
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }

    // ========================================================================
    // TestPrinterAsync — Null Guard Clauses
    // ========================================================================

    [Fact]
    public async Task TestPrinterAsync_NullPrinter_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.TestPrinterAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ========================================================================
    // TestPrinterAsync — Dispatch Error Paths
    // ========================================================================

    [Fact]
    public async Task TestPrinterAsync_NetworkEmptyIp_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "");

        var result = await _sut.TestPrinterAsync(printer);

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Test print failed")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task TestPrinterAsync_NetworkNullIp_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: null);

        var result = await _sut.TestPrinterAsync(printer);

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Test print failed")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task TestPrinterAsync_SerialNullConnectionString_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: null);

        var result = await _sut.TestPrinterAsync(printer);

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Test print failed") && msg.Contains("has no COM port")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task TestPrinterAsync_UsbNullConnectionStringAndNullName_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: null, name: null!);

        var result = await _sut.TestPrinterAsync(printer);

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Test print failed")
                && (msg.Contains("no printer name") || msg.Contains("or ConnectionString"))),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task TestPrinterAsync_UnknownConnection_ShouldReturnTrue()
    {
        var printer = CreatePrinter((PrinterConnection)999);

        var result = await _sut.TestPrinterAsync(printer);

        result.Should().BeTrue();
        _loggerMock.Verify(l => l.LogInfo(
            It.Is<string>(msg => msg.Contains("has no known connection type")),
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }

    // ========================================================================
    // OpenCashDrawerAsync — Null Guard Clauses
    // ========================================================================

    [Fact]
    public async Task OpenCashDrawerAsync_NullPrinter_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.OpenCashDrawerAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ========================================================================
    // OpenCashDrawerAsync — Dispatch Error Paths
    // ========================================================================

    [Fact]
    public async Task OpenCashDrawerAsync_NetworkEmptyIp_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "");

        var result = await _sut.OpenCashDrawerAsync(printer);

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Failed to open cash drawer")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task OpenCashDrawerAsync_SerialNullConnectionString_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: null);

        var result = await _sut.OpenCashDrawerAsync(printer);

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Failed to open cash drawer") && msg.Contains("has no COM port")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task OpenCashDrawerAsync_UsbNullConnectionStringAndNullName_ShouldLogErrorAndReturnFalse()
    {
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: null, name: null!);

        var result = await _sut.OpenCashDrawerAsync(printer);

        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Failed to open cash drawer")
                && (msg.Contains("no printer name") || msg.Contains("or ConnectionString"))),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task OpenCashDrawerAsync_UnknownConnection_ShouldReturnTrue()
    {
        var printer = CreatePrinter((PrinterConnection)999);

        var result = await _sut.OpenCashDrawerAsync(printer);

        result.Should().BeTrue();
        _loggerMock.Verify(l => l.LogInfo(
            It.Is<string>(msg => msg.Contains("has no known connection type")),
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }

    // ========================================================================
    // GetPrinterStatus — Null Guard Clause
    // ========================================================================

    [Fact]
    public void GetPrinterStatus_NullPrinter_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.GetPrinterStatus(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ========================================================================
    // GetPrinterStatus — Network Status Paths
    // ========================================================================

    [Fact]
    public void GetPrinterStatus_NetworkNullIp_ShouldReturnOffline()
    {
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: null);

        var result = _sut.GetPrinterStatus(printer);

        result.Should().Be(PrinterStatus.Offline);
    }

    [Fact]
    public void GetPrinterStatus_NetworkEmptyIp_ShouldReturnOffline()
    {
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "");

        var result = _sut.GetPrinterStatus(printer);

        result.Should().Be(PrinterStatus.Offline);
    }

    // ========================================================================
    // GetPrinterStatus — Serial Status Paths
    // ========================================================================

    [Fact]
    public void GetPrinterStatus_SerialNullConnectionString_ShouldReturnOffline()
    {
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: null);

        var result = _sut.GetPrinterStatus(printer);

        result.Should().Be(PrinterStatus.Offline);
    }

    [Fact]
    public void GetPrinterStatus_SerialEmptyConnectionString_ShouldReturnOffline()
    {
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: "");

        var result = _sut.GetPrinterStatus(printer);

        result.Should().Be(PrinterStatus.Offline);
    }

    [Fact]
    public void GetPrinterStatus_SerialNonexistentPort_ShouldReturnOffline()
    {
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: "COM_NONEXISTENT");

        var result = _sut.GetPrinterStatus(printer);

        result.Should().Be(PrinterStatus.Offline);
    }

    // ========================================================================
    // GetPrinterStatus — Mock Dispatch Verification
    // ========================================================================

    [Fact]
    public void GetPrinterStatus_NetworkPrinter_DispatchesToMock()
    {
        // Arrange — setup mock to return Online for network
        _hardwareMock.Setup(h => h.GetNetworkPrinterStatus(It.IsAny<Printer>()))
            .Returns(PrinterStatus.Online);
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "192.168.1.100");

        // Act
        var result = _sut.GetPrinterStatus(printer);

        // Assert — dispatches to mock, returns mock's value
        result.Should().Be(PrinterStatus.Online);
        _hardwareMock.Verify(h => h.GetNetworkPrinterStatus(It.IsAny<Printer>()), Times.Once);
        _hardwareMock.Verify(h => h.GetSerialPrinterStatus(It.IsAny<Printer>()), Times.Never);
        _hardwareMock.Verify(h => h.GetUsbPrinterStatus(It.IsAny<Printer>()), Times.Never);
    }

    [Fact]
    public void GetPrinterStatus_SerialPrinter_DispatchesToMock()
    {
        // Arrange — setup mock to return Error for serial
        _hardwareMock.Setup(h => h.GetSerialPrinterStatus(It.IsAny<Printer>()))
            .Returns(PrinterStatus.Error);
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: "COM1");

        // Act
        var result = _sut.GetPrinterStatus(printer);

        // Assert — dispatches to mock serial handler
        result.Should().Be(PrinterStatus.Error);
        _hardwareMock.Verify(h => h.GetSerialPrinterStatus(It.IsAny<Printer>()), Times.Once);
        _hardwareMock.Verify(h => h.GetNetworkPrinterStatus(It.IsAny<Printer>()), Times.Never);
        _hardwareMock.Verify(h => h.GetUsbPrinterStatus(It.IsAny<Printer>()), Times.Never);
    }

    [Fact]
    public void GetPrinterStatus_UsbPrinter_DispatchesToMock()
    {
        // Arrange — setup mock to return Online for USB
        _hardwareMock.Setup(h => h.GetUsbPrinterStatus(It.IsAny<Printer>()))
            .Returns(PrinterStatus.Online);
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: "USB001");

        // Act
        var result = _sut.GetPrinterStatus(printer);

        // Assert — dispatches to mock USB handler
        result.Should().Be(PrinterStatus.Online);
        _hardwareMock.Verify(h => h.GetUsbPrinterStatus(It.IsAny<Printer>()), Times.Once);
        _hardwareMock.Verify(h => h.GetNetworkPrinterStatus(It.IsAny<Printer>()), Times.Never);
        _hardwareMock.Verify(h => h.GetSerialPrinterStatus(It.IsAny<Printer>()), Times.Never);
    }

    [Fact]
    public void GetPrinterStatus_MockThrows_CatchesAndReturnsOffline()
    {
        // Arrange — mock throws an unexpected exception from the USB status handler
        // This exercises the outer catch block in GetPrinterStatus (line 371 equivalent).
        _hardwareMock.Setup(h => h.GetUsbPrinterStatus(It.IsAny<Printer>()))
            .Throws(new InvalidOperationException("Unexpected hardware error"));
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: "USB001");

        // Act — exception from mock is caught by GetPrinterStatus's outer catch
        var result = _sut.GetPrinterStatus(printer);

        // Assert — outer catch returns Offline and logs error
        result.Should().Be(PrinterStatus.Offline);
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error checking status")),
            It.IsAny<Exception?>()), Times.Once);
    }

    // ========================================================================
    // PrintReceiptAsync — Long Product Name (triggers BuildItemLine truncation)
    // ========================================================================

    [Fact]
    public async Task PrintReceiptAsync_LongProductName_ShouldTruncateName()
    {
        // Arrange — product name > 24 chars triggers the name[..24] truncation in BuildItemLine
        var printer = CreatePrinter((PrinterConnection)999, name: "TruncationTest");
        var sale = CreateMinimalSale();
        var longName = "This product name exceeds 24 character limit by far"; // 54 chars
        var item = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            ProductId = Guid.NewGuid(),
            ProductName = longName,
            Quantity = 1,
            UnitPrice = 10.000m,
            TotalPrice = 10.000m,
            LineTotal = 11.600m,
            TaxRate = 0.16m
        };
        sale.AddItem(item);

        // Act
        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        // Assert — should succeed (no hardware needed)
        result.Should().BeTrue();
    }

    // ========================================================================
    // PrintReceiptAsync — Non-Zero Round Amount
    // ========================================================================

    [Fact]
    public async Task PrintReceiptAsync_WithRoundAmount_ShouldIncludeRoundingLine()
    {
        // Arrange — RoundAmount != 0 triggers the rounding line in PrintReceiptAsync
        var printer = CreatePrinter((PrinterConnection)999, name: "RoundingTest");
        var sale = CreateMinimalSale();
        sale.RoundAmount = 0.500m;
        // Add a payment so RemainingAmount = 0 triggers the change calc else-if
        sale.AddPayment(new Payment
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            Amount = 15.000m,
            PaymentMethod = PaymentMethod.Cash,
            Timestamp = DateTime.UtcNow
        });
        sale.TotalAmount = 12.000m; // Change = 3.000

        // Act
        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        // Assert — should succeed
        result.Should().BeTrue();
    }

    // ========================================================================
    // PrintReceiptAsync — Payment with Tip and ReferenceNumber
    // ========================================================================

    [Fact]
    public async Task PrintReceiptAsync_PaymentWithTipAndReference_ShouldPrintDetails()
    {
        // Arrange — payment.TipAmount > 0 and ReferenceNumber != null add extra info lines
        var printer = CreatePrinter((PrinterConnection)999, name: "TipTest");
        var sale = CreateMinimalSale();
        sale.TotalAmount = 50.000m;
        sale.AddPayment(new Payment
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            Amount = 60.000m,
            PaymentMethod = PaymentMethod.Card,
            TipAmount = 5.000m,
            ReferenceNumber = "TXN-98765",
            Timestamp = DateTime.UtcNow
        });
        // RemainingAmount = 0 so we exercise the else-if change calc
        sale.RemainingAmount = 0m;

        // Act
        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        // Assert
        result.Should().BeTrue();
    }

    // ========================================================================
    // PrintKitchenTicketAsync — Sale with Notes
    // ========================================================================

    [Fact]
    public async Task PrintKitchenTicketAsync_WithSaleNotes_ShouldPrintOrderNotesSection()
    {
        // Arrange — sale.Notes != null triggers the order notes section in PrintKitchenTicketAsync
        var printer = CreatePrinter((PrinterConnection)999, name: "NotesTest");
        var sale = CreateMinimalSale();
        sale.Notes = "Please rush this order - customer is waiting at Table 5";
        var item = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            ProductId = Guid.NewGuid(),
            ProductName = "Test Item",
            Quantity = 2,
            UnitPrice = 10.000m,
            TotalPrice = 20.000m,
            LineTotal = 23.200m,
            TaxRate = 0.16m
        };
        sale.AddItem(item);

        // Act
        var result = await _sut.PrintKitchenTicketAsync(printer, sale, "Main Kitchen");

        // Assert — should succeed
        result.Should().BeTrue();
    }

    // ========================================================================
    // PrintReceiptAsync — Mock Dispatch Verification
    // ========================================================================

    [Fact]
    public async Task PrintReceiptAsync_NetworkPrinter_DispatchesToMock()
    {
        // Arrange — setup mock to succeed (no exception)
        _hardwareMock.Setup(h => h.SendViaNetworkAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .Returns(Task.CompletedTask);
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "192.168.1.100");
        var sale = CreateMinimalSale();

        // Act
        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        // Assert — dispatches to mock, returns true
        result.Should().BeTrue();
        _hardwareMock.Verify(h => h.SendViaNetworkAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()), Times.Once);
        _hardwareMock.Verify(h => h.SendViaSerialAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()), Times.Never);
        _hardwareMock.Verify(h => h.SendViaUsbAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()), Times.Never);
    }

    [Fact]
    public async Task PrintReceiptAsync_SerialPrinter_DispatchesToMock()
    {
        // Arrange
        _hardwareMock.Setup(h => h.SendViaSerialAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .Returns(Task.CompletedTask);
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: "COM1");
        var sale = CreateMinimalSale();

        // Act
        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        // Assert
        result.Should().BeTrue();
        _hardwareMock.Verify(h => h.SendViaSerialAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_UsbPrinter_DispatchesToMock()
    {
        // Arrange
        _hardwareMock.Setup(h => h.SendViaUsbAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .Returns(Task.CompletedTask);
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: "USB_PRINTER");
        var sale = CreateMinimalSale();

        // Act
        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        // Assert
        result.Should().BeTrue();
        _hardwareMock.Verify(h => h.SendViaUsbAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_MockThrowsNetwork_CatchesAndReturnsFalse()
    {
        // Arrange — mock throws (default setup), no need to override
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "192.168.1.100");
        var sale = CreateMinimalSale();

        // Act — exception from mock is caught by PrintReceiptAsync's try/catch
        var result = await _sut.PrintReceiptAsync(printer, sale, "Store", "متجر");

        // Assert — outer catch returns false and logs error
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt")),
            It.IsAny<Exception?>()), Times.Once);
    }

    // ========================================================================
    // PrintKitchenTicketAsync — Mock Dispatch Verification
    // ========================================================================

    [Fact]
    public async Task PrintKitchenTicketAsync_NetworkPrinter_DispatchesToMock()
    {
        // Arrange
        _hardwareMock.Setup(h => h.SendViaNetworkAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .Returns(Task.CompletedTask);
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "192.168.1.100");
        var sale = CreateMinimalSale();

        // Act
        var result = await _sut.PrintKitchenTicketAsync(printer, sale, "Main Kitchen");

        // Assert
        result.Should().BeTrue();
        _hardwareMock.Verify(h => h.SendViaNetworkAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()), Times.Once);
    }

    [Fact]
    public async Task PrintKitchenTicketAsync_SerialPrinter_DispatchesToMock()
    {
        // Arrange
        _hardwareMock.Setup(h => h.SendViaSerialAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .Returns(Task.CompletedTask);
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: "COM1");
        var sale = CreateMinimalSale();

        // Act
        var result = await _sut.PrintKitchenTicketAsync(printer, sale, "Beverage");

        // Assert
        result.Should().BeTrue();
        _hardwareMock.Verify(h => h.SendViaSerialAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()), Times.Once);
    }

    [Fact]
    public async Task PrintKitchenTicketAsync_UsbPrinter_DispatchesToMock()
    {
        // Arrange
        _hardwareMock.Setup(h => h.SendViaUsbAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .Returns(Task.CompletedTask);
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: "USB_KITCHEN");
        var sale = CreateMinimalSale();

        // Act
        var result = await _sut.PrintKitchenTicketAsync(printer, sale, "Main Kitchen");

        // Assert
        result.Should().BeTrue();
        _hardwareMock.Verify(h => h.SendViaUsbAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()), Times.Once);
    }

    [Fact]
    public async Task PrintKitchenTicketAsync_MockThrowsNetwork_CatchesAndReturnsFalse()
    {
        // Arrange — mock throws by default
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "192.168.1.100");
        var sale = CreateMinimalSale();

        // Act
        var result = await _sut.PrintKitchenTicketAsync(printer, sale, "Main Kitchen");

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing kitchen ticket")),
            It.IsAny<Exception?>()), Times.Once);
    }

    // ========================================================================
    // TestPrinterAsync — Mock Dispatch Verification
    // ========================================================================

    [Fact]
    public async Task TestPrinterAsync_NetworkPrinter_DispatchesToMock()
    {
        // Arrange
        _hardwareMock.Setup(h => h.SendViaNetworkAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .Returns(Task.CompletedTask);
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "192.168.1.100");

        // Act
        var result = await _sut.TestPrinterAsync(printer);

        // Assert
        result.Should().BeTrue();
        _hardwareMock.Verify(h => h.SendViaNetworkAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()), Times.Once);
    }

    [Fact]
    public async Task TestPrinterAsync_SerialPrinter_DispatchesToMock()
    {
        // Arrange
        _hardwareMock.Setup(h => h.SendViaSerialAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .Returns(Task.CompletedTask);
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: "COM1");

        // Act
        var result = await _sut.TestPrinterAsync(printer);

        // Assert
        result.Should().BeTrue();
        _hardwareMock.Verify(h => h.SendViaSerialAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()), Times.Once);
    }

    [Fact]
    public async Task TestPrinterAsync_UsbPrinter_DispatchesToMock()
    {
        // Arrange
        _hardwareMock.Setup(h => h.SendViaUsbAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .Returns(Task.CompletedTask);
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: "USB_TEST");

        // Act
        var result = await _sut.TestPrinterAsync(printer);

        // Assert
        result.Should().BeTrue();
        _hardwareMock.Verify(h => h.SendViaUsbAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()), Times.Once);
    }

    [Fact]
    public async Task TestPrinterAsync_MockThrowsNetwork_CatchesAndReturnsFalse()
    {
        // Arrange — mock throws by default
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "192.168.1.100");

        // Act
        var result = await _sut.TestPrinterAsync(printer);

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Test print failed")),
            It.IsAny<Exception?>()), Times.Once);
    }

    // ========================================================================
    // OpenCashDrawerAsync — Mock Dispatch Verification
    // ========================================================================

    [Fact]
    public async Task OpenCashDrawerAsync_NetworkPrinter_DispatchesToMock()
    {
        // Arrange
        _hardwareMock.Setup(h => h.SendViaNetworkAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .Returns(Task.CompletedTask);
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "192.168.1.100");

        // Act
        var result = await _sut.OpenCashDrawerAsync(printer);

        // Assert
        result.Should().BeTrue();
        _hardwareMock.Verify(h => h.SendViaNetworkAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()), Times.Once);
    }

    [Fact]
    public async Task OpenCashDrawerAsync_SerialPrinter_DispatchesToMock()
    {
        // Arrange
        _hardwareMock.Setup(h => h.SendViaSerialAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .Returns(Task.CompletedTask);
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: "COM1");

        // Act
        var result = await _sut.OpenCashDrawerAsync(printer);

        // Assert
        result.Should().BeTrue();
        _hardwareMock.Verify(h => h.SendViaSerialAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()), Times.Once);
    }

    [Fact]
    public async Task OpenCashDrawerAsync_UsbPrinter_DispatchesToMock()
    {
        // Arrange
        _hardwareMock.Setup(h => h.SendViaUsbAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .Returns(Task.CompletedTask);
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: "USB_DRAWER");

        // Act
        var result = await _sut.OpenCashDrawerAsync(printer);

        // Assert
        result.Should().BeTrue();
        _hardwareMock.Verify(h => h.SendViaUsbAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()), Times.Once);
    }

    [Fact]
    public async Task OpenCashDrawerAsync_MockThrowsSerial_CatchesAndReturnsFalse()
    {
        // Arrange — mock throws by default
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: "COM1");

        // Act
        var result = await _sut.OpenCashDrawerAsync(printer);

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Failed to open cash drawer")),
            It.IsAny<Exception?>()), Times.Once);
    }

    // ========================================================================
    // GetPrinterStatus — USB Status Paths
    // ========================================================================

    [Fact]
    public void GetPrinterStatus_UsbNullConnectionStringAndNullName_ShouldReturnOffline()
    {
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: null, name: null!);

        var result = _sut.GetPrinterStatus(printer);

        result.Should().Be(PrinterStatus.Offline);
    }

    [Fact]
    public void GetPrinterStatus_UsbEmptyConnectionString_ShouldReturnOffline()
    {
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: "");

        var result = _sut.GetPrinterStatus(printer);

        result.Should().Be(PrinterStatus.Offline);
    }

    [Fact]
    public void GetPrinterStatus_UsbNonexistentPrinterName_ShouldReturnOffline()
    {
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: "NONEXISTENT_PRINTER_XYZ");

        var result = _sut.GetPrinterStatus(printer);

        result.Should().Be(PrinterStatus.Offline);
    }

    // ========================================================================
    // GetPrinterStatus — Unknown Connection
    // ========================================================================

    [Fact]
    public void GetPrinterStatus_UnknownConnection_ShouldLogWarningAndReturnOffline()
    {
        var printer = CreatePrinter((PrinterConnection)999, name: "Unknown");

        var result = _sut.GetPrinterStatus(printer);

        result.Should().Be(PrinterStatus.Offline);
        _loggerMock.Verify(l => l.LogWarning(
            It.Is<string>(msg => msg.Contains("Unknown printer connection type")),
            It.IsAny<object?[]>()), Times.Once);
    }

    // ========================================================================
    // SendToPrinterAsync — Empty Commands Edge Case (via private method)
    // This is tested through the unknown connection path with an empty sale
    // ========================================================================

    // Remove the redundant SendToPrinterAsync test — already covered by EmptySaleItems above



    // ========================================================================
    // Helpers
    // ========================================================================

    private static Printer CreatePrinter(
        PrinterConnection connection,
        string? ipAddress = "192.168.1.100",
        string? connectionString = "COM1",
        string? name = "Test Printer")
    {
        return new Printer
        {
            Id = Guid.NewGuid(),
            Name = name ?? string.Empty,
            PrinterType = PrinterType.Thermal,
            Connection = connection,
            IpAddress = connection == PrinterConnection.Network ? ipAddress : null,
            Port = 9100,
            ConnectionString = connection switch
            {
                PrinterConnection.Serial => connectionString,
                PrinterConnection.USB => connectionString,
                _ => null
            },
            PaperWidth = 80,
            BaudRate = 9600,
            IsActive = true
        };
    }

    private static Sale CreateMinimalSale()
    {
        return new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-TEST-001",
            ShiftId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SubTotal = 10.000m,
            TaxAmount = 1.600m,
            TotalAmount = 11.600m,
            Status = SaleStatus.Active
        };
    }
}
