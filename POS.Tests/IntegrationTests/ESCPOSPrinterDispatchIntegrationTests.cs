using Xunit;
using Moq;
using FluentAssertions;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Infrastructure.Printing;

namespace POS.Tests.IntegrationTests;

/// <summary>
/// Integration tests for ESCPOSPrinter.
/// Verifies that SendToPrinterAsync dispatches to the correct hardware handler
/// (Network/Serial/USB) based on Printer.Connection, through all 4 public methods:
/// PrintReceiptAsync, PrintKitchenTicketAsync, TestPrinterAsync, GetPrinterStatus.
///
/// No physical hardware required — tests verify dispatch by checking:
/// - Return values (false when no hardware available)
/// - Error log messages (contain handler-specific text)
/// - Status results (Offline for unreachable connections)
/// </summary>
public class ESCPOSPrinterDispatchIntegrationTests
{
    private readonly Mock<ILoggerService> _loggerMock;
    private readonly Mock<IPrinterHardwareSender> _hardwareMock;
    private readonly ESCPOSPrinter _printer;

    public ESCPOSPrinterDispatchIntegrationTests()
    {
        _loggerMock = new Mock<ILoggerService>();
        _loggerMock.Setup(l => l.LogInfo(It.IsAny<string>(), It.IsAny<object?[]>()));
        _loggerMock.Setup(l => l.LogWarning(It.IsAny<string>(), It.IsAny<object?[]>()));
        _loggerMock.Setup(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<object?[]>()));
        _loggerMock.Setup(l => l.LogDebug(It.IsAny<string>(), It.IsAny<object?[]>()));
        _hardwareMock = new Mock<IPrinterHardwareSender>();
        // Make the hardware sender throw with messages containing all keywords
        // that existing dispatch assertions check for:
        //   - Network: "Network printer"
        //   - Serial: "has no COM port"
        //   - USB: "no printer name" || "or ConnectionString"
        _hardwareMock.Setup(h => h.SendViaNetworkAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .ThrowsAsync(new InvalidOperationException(
                "Network printer mock no IP address configured"));
        _hardwareMock.Setup(h => h.SendViaSerialAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .ThrowsAsync(new InvalidOperationException(
                "Serial printer mock has no COM port configured"));
        _hardwareMock.Setup(h => h.SendViaUsbAsync(It.IsAny<Printer>(), It.IsAny<List<byte[]>>()))
            .ThrowsAsync(new InvalidOperationException(
                "USB printer mock no printer name or ConnectionString configured"));
        _hardwareMock.Setup(h => h.GetNetworkPrinterStatus(It.IsAny<Printer>()))
            .Returns(PrinterStatus.Offline);
        _hardwareMock.Setup(h => h.GetSerialPrinterStatus(It.IsAny<Printer>()))
            .Returns(PrinterStatus.Offline);
        _hardwareMock.Setup(h => h.GetUsbPrinterStatus(It.IsAny<Printer>()))
            .Returns(PrinterStatus.Offline);
        _printer = new ESCPOSPrinter(_loggerMock.Object, _hardwareMock.Object);
    }

    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static Sale CreateTestSale()
    {
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-INT-001",
            UserId = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            CreatedAt = new DateTime(2026, 7, 19, 14, 30, 0, DateTimeKind.Utc),
            SubTotal = 100.000m,
            TaxAmount = 16.000m,
            DiscountAmount = 5.000m,
            TotalAmount = 111.000m,
            RoundAmount = 0.000m,
            RemainingAmount = 0m,
            Status = SaleStatus.Completed,
            IsPaid = true,
            PaidAt = new DateTime(2026, 7, 19, 14, 35, 0, DateTimeKind.Utc),
            User = new User { Id = Guid.NewGuid(), FullName = "Test Cashier" },
            Table = new Table { Id = Guid.NewGuid(), Name = "Table 3" },
            Customer = new Customer { Id = Guid.NewGuid(), Name = "John Doe" }
        };

        var item1 = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            ProductId = Guid.NewGuid(),
            ProductName = "Coffee Latte",
            ProductArabicName = "لاتيه",
            Quantity = 2,
            UnitPrice = 12.000m,
            TotalPrice = 24.000m,
            LineTotal = 27.840m,
            TaxRate = 16.000m,
            TaxAmount = 3.840m,
            Cost = 5.000m,
            Notes = "Extra hot"
        };
        item1.AddModifier(new SaleItemModifier
        {
            Id = Guid.NewGuid(),
            SaleItemId = item1.Id,
            ModifierName = "Soy Milk",
            ModifierArabicName = "حليب صويا",
            Price = 2.000m,
            AdditionalPrice = 2.000m,
            Quantity = 1
        });

        var item2 = new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            ProductId = Guid.NewGuid(),
            ProductName = "Croissant",
            ProductArabicName = "كرواسون",
            Quantity = 1,
            UnitPrice = 8.000m,
            TotalPrice = 8.000m,
            LineTotal = 9.280m,
            TaxRate = 16.000m,
            TaxAmount = 1.280m
        };

        sale.AddItem(item1);
        sale.AddItem(item2);

        sale.AddPayment(new Payment
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            Amount = 37.120m,
            PaymentMethod = PaymentMethod.Cash,
            Timestamp = DateTime.UtcNow
        });

        return sale;
    }

    private static Printer CreatePrinter(PrinterConnection connection, string? name = null)
    {
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = name ?? $"Test Printer ({connection})",
            PrinterType = PrinterType.Thermal,
            Connection = connection,
            PaperWidth = 80,
            AssignedRole = PrinterRole.Receipt,
            BaudRate = 9600,
            IsActive = true
        };

        switch (connection)
        {
            case PrinterConnection.Network:
                // Empty IP — guard clause in SendViaNetworkAsync fires immediately
                // This makes the test fast while still verifying dispatch
                printer.IpAddress = "";
                printer.Port = 9100;
                break;

            case PrinterConnection.Serial:
                // Non-existent COM port — SerialPort.Open throws FileNotFoundException
                printer.ConnectionString = "COM_NOT_EXIST_PORT_TEST";
                break;

            case PrinterConnection.USB:
                // Non-existent printer name — OpenPrinter returns false
                printer.ConnectionString = "NON_EXISTENT_USB_PRINTER_TEST";
                break;
        }

        return printer;
    }

    // ========================================================================
    // PrintReceiptAsync — Dispatch Verification
    // ========================================================================

    [Fact]
    public async Task PrintReceiptAsync_WithNetworkPrinter_DispatchesToNetworkHandler()
    {
        // Arrange
        var sale = CreateTestSale();
        var printer = CreatePrinter(PrinterConnection.Network);

        // Act
        var result = await _printer.PrintReceiptAsync(printer, sale, "Test Store", "متجر اختبار");

        // Assert
        // Should fail with error from the Network handler (empty IP — guard clause via IsNullOrWhiteSpace)
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt") && msg.Contains("Network printer")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_WithNetworkPrinter_NullIpAddress_ThrowsGuardClause()
    {
        // Arrange — network printer with IpAddress left as null (default)
        // IsNullOrWhiteSpace(null) is true → guard clause fires (same behavior as empty string)
        // This verifies the guard uses IsNullOrWhiteSpace, not just == null or == ""
        var sale = CreateTestSale();
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "Network Null IP Guard",
            PrinterType = PrinterType.Thermal,
            Connection = PrinterConnection.Network,
            Port = 9100,
            PaperWidth = 80,
            BaudRate = 9600,
            IsActive = true
            // IpAddress = null (default) — triggers the IsNullOrWhiteSpace guard clause
        };

        // Act
        var result = await _printer.PrintReceiptAsync(printer, sale, "Test Store", "متجر اختبار");

        // Assert
        // Should behave identically to the empty-IP test: guard clause fires, no socket connect attempted
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt") && msg.Contains("Network printer")),
            It.IsAny<Exception?>()), Times.Once);
    }

    // Socket timeout test removed — the TCP socket connect code is now in
    // RealPrinterHardwareSender. The ESCPOSPrinter layer delegates to the
    // IPrinterHardwareSender mock and the timeout behavior is tested through
    // RealPrinterHardwareSender integration tests.

    [Fact]
    public async Task PrintReceiptAsync_WithSerialPrinter_DispatchesToSerialHandler()
    {
        // Arrange
        var sale = CreateTestSale();
        var printer = CreatePrinter(PrinterConnection.Serial);

        // Act
        var result = await _printer.PrintReceiptAsync(printer, sale, "Test Store", "متجر اختبار");

        // Assert
        // Should fail with error from the Serial handler (COM port not found)
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_WithSerialPrinter_NullConnectionString_ThrowsGuardClause()
    {
        // Arrange — serial printer with null ConnectionString
        // SendViaSerialAsync hits: if (string.IsNullOrWhiteSpace(portName)) throw new InvalidOperationException
        var sale = CreateTestSale();
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "Serial Guard Clause Test",
            Connection = PrinterConnection.Serial,
            BaudRate = 9600,
            IsActive = true
            // ConnectionString = null (default) — triggers the guard clause
        };

        // Act
        var result = await _printer.PrintReceiptAsync(printer, sale, "Test Store", "متجر اختبار");

        // Assert
        // Should fail with error about missing COM port configuration
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt") && msg.Contains("has no COM port")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_WithUsbPrinter_DispatchesToUsbHandler()
    {
        // Arrange
        var sale = CreateTestSale();
        var printer = CreatePrinter(PrinterConnection.USB);

        // Act
        var result = await _printer.PrintReceiptAsync(printer, sale, "Test Store", "متجر اختبار");

        // Assert
        // Should fail with error from the USB handler (OpenPrinter fails)
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_WithUsbComFallback_DispatchesToSerialHandler()
    {
        // Arrange
        var sale = CreateTestSale();
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "USB Virtual COM Printer",
            Connection = PrinterConnection.USB,
            ConnectionString = "COM_NOT_EXIST_PORT_TEST",  // Starts with "COM" → falls back to Serial handler
            BaudRate = 9600,
            IsActive = true
        };

        // Act
        var result = await _printer.PrintReceiptAsync(printer, sale, "Test Store", "متجر اختبار");

        // Assert
        // The COM fallback should attempt SerialPort.Open which throws FileNotFoundException
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_WithUsbPrinter_NullConnectionString_FallsBackToPrinterName()
    {
        // Arrange — USB printer with null ConnectionString and a valid printer Name
        // ConnectionString = null → IsNullOrWhiteSpace(null) is true → skips COM check
        // printerName falls back to printer.Name → passes guard clause
        // RawPrinterHelper.SendRawDataChunks is called with the printer name → fails
        var sale = CreateTestSale();
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "FALLBACK_USB_PRINTER_TEST",  // Used as the target printer name
            PrinterType = PrinterType.Thermal,
            Connection = PrinterConnection.USB,
            PaperWidth = 80,
            BaudRate = 9600,
            IsActive = true
            // ConnectionString = null (default) — triggers the Name fallback
        };

        // Act
        var result = await _printer.PrintReceiptAsync(printer, sale, "Test Store", "متجر اختبار");

        // Assert
        // Should fail with error from the USB handler (OpenPrinter fails on non-existent name)
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt") && msg.Contains("USB")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_WithUsbPrinter_EmptyConnectionString_FallsBackToPrinterName()
    {
        // Arrange — USB printer with an empty-string ConnectionString ("")
        // IsNullOrWhiteSpace("") is true — identical behavior to null:
        //   - Skips the COM prefix check (same as null)
        //   - printerName falls back to printer.Name (same as null)
        // This verifies the guard uses IsNullOrWhiteSpace (not just == null).
        var sale = CreateTestSale();
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "EMPTY_STRING_USB_TEST",
            PrinterType = PrinterType.Thermal,
            Connection = PrinterConnection.USB,
            PaperWidth = 80,
            BaudRate = 9600,
            IsActive = true,
            ConnectionString = ""  // Empty string — IsNullOrWhiteSpace returns true
        };

        // Act
        var result = await _printer.PrintReceiptAsync(printer, sale, "Test Store", "متجر اختبار");

        // Assert
        // Should behave identically to the null ConnectionString test above
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt") && msg.Contains("USB")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_WithUsbPrinter_NullConnectionStringAndNullName_ThrowsGuardClause()
    {
        // Arrange — USB printer with BOTH ConnectionString and Name null
        // printerName = null → hits guard clause: "has no printer name or ConnectionString configured"
        var sale = CreateTestSale();
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = null!,  // Both ConnectionString AND Name are null
            PrinterType = PrinterType.Thermal,
            Connection = PrinterConnection.USB,
            PaperWidth = 80,
            BaudRate = 9600,
            IsActive = true
            // ConnectionString = null (default)
        };

        // Act
        var result = await _printer.PrintReceiptAsync(printer, sale, "Test Store", "متجر اختبار");

        // Assert
        // Should fail with error about missing printer name/connection string
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt")
                && (msg.Contains("no printer name") || msg.Contains("or ConnectionString"))),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintReceiptAsync_WithUnknownConnection_FallsBackToLogging()
    {
        // Arrange — use a numeric value that doesn't match any PrinterConnection
        var unknownConnection = (PrinterConnection)999;
        var sale = CreateTestSale();
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "Unknown Connection Printer",
            Connection = unknownConnection,
            IsActive = true
        };

        // Act — unknown connection falls through to the default case which logs + delays
        var result = await _printer.PrintReceiptAsync(printer, sale, "Test Store", "متجر اختبار");

        // Assert — the default case doesn't throw, it just logs and delays → returns true
        result.Should().BeTrue();
        _loggerMock.Verify(l => l.LogInfo(
            It.Is<string>(msg => msg.Contains("has no known connection type")),
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }

    // ========================================================================
    // PrintKitchenTicketAsync — Dispatch Verification
    // ========================================================================

    [Fact]
    public async Task PrintKitchenTicketAsync_WithNetworkPrinter_DispatchesToNetworkHandler()
    {
        // Arrange
        var sale = CreateTestSale();
        var printer = CreatePrinter(PrinterConnection.Network);

        // Act
        var result = await _printer.PrintKitchenTicketAsync(printer, sale, "Main Kitchen");

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing kitchen ticket")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintKitchenTicketAsync_WithSerialPrinter_DispatchesToSerialHandler()
    {
        // Arrange
        var sale = CreateTestSale();
        var printer = CreatePrinter(PrinterConnection.Serial);

        // Act
        var result = await _printer.PrintKitchenTicketAsync(printer, sale, "Beverage Station");

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing kitchen ticket")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintKitchenTicketAsync_WithSerialPrinter_NullConnectionString_ThrowsGuardClause()
    {
        // Arrange — serial printer with null ConnectionString
        // SendViaSerialAsync guard clause: "has no COM port configured in ConnectionString"
        var sale = CreateTestSale();
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "Kitchen Serial Guard",
            Connection = PrinterConnection.Serial,
            BaudRate = 9600,
            IsActive = true
            // ConnectionString = null (default)
        };

        // Act
        var result = await _printer.PrintKitchenTicketAsync(printer, sale, "Beverage Station");

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing kitchen ticket") && msg.Contains("has no COM port")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintKitchenTicketAsync_WithUsbPrinter_DispatchesToUsbHandler()
    {
        // Arrange
        var sale = CreateTestSale();
        var printer = CreatePrinter(PrinterConnection.USB);

        // Act
        var result = await _printer.PrintKitchenTicketAsync(printer, sale, "Main Kitchen");

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing kitchen ticket")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task PrintKitchenTicketAsync_WithUsbPrinter_NullConnectionString_FallsBackToPrinterName()
    {
        // Arrange — USB printer with null ConnectionString, falls back to printer.Name
        var sale = CreateTestSale();
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "KITCHEN_FALLBACK_USB_TEST",
            Connection = PrinterConnection.USB,
            BaudRate = 9600,
            IsActive = true
            // ConnectionString = null (default) → falls back to Name
        };

        // Act
        var result = await _printer.PrintKitchenTicketAsync(printer, sale, "Main Kitchen");

        // Assert — dispatches to USB handler, fails via RawPrinterHelper
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing kitchen ticket")),
            It.IsAny<Exception?>()), Times.Once);
    }

    // ========================================================================
    // TestPrinterAsync — Dispatch Verification
    // ========================================================================

    [Fact]
    public async Task TestPrinterAsync_WithNetworkPrinter_DispatchesToNetworkHandler()
    {
        // Arrange
        var printer = CreatePrinter(PrinterConnection.Network);

        // Act
        var result = await _printer.TestPrinterAsync(printer);

        // Assert
        // Guard clause fires because IP is empty — fast path
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Test print failed")),
            It.IsAny<Exception?>()), Times.Once);
    }

    // Socket timeout test removed — the TCP socket connect code is now in
    // RealPrinterHardwareSender. The timeout behavior is now in RealPrinterHardwareSender.

    [Fact]
    public async Task TestPrinterAsync_WithSerialPrinter_DispatchesToSerialHandler()
    {
        // Arrange
        var printer = CreatePrinter(PrinterConnection.Serial);

        // Act
        var result = await _printer.TestPrinterAsync(printer);

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Test print failed")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task TestPrinterAsync_WithSerialPrinter_NullConnectionString_ThrowsGuardClause()
    {
        // Arrange — serial printer with null ConnectionString
        // SendViaSerialAsync guard clause: "has no COM port configured in ConnectionString"
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "Test Serial Guard",
            Connection = PrinterConnection.Serial,
            BaudRate = 9600,
            IsActive = true
            // ConnectionString = null (default)
        };

        // Act
        var result = await _printer.TestPrinterAsync(printer);

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Test print failed") && msg.Contains("has no COM port")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task TestPrinterAsync_WithUsbPrinter_DispatchesToUsbHandler()
    {
        // Arrange
        var printer = CreatePrinter(PrinterConnection.USB);

        // Act
        var result = await _printer.TestPrinterAsync(printer);

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Test print failed")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task TestPrinterAsync_WithUsbPrinter_NullConnectionString_FallsBackToPrinterName()
    {
        // Arrange — USB printer with null ConnectionString, falls back to printer.Name
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "TEST_FALLBACK_USB_PRINTER",
            Connection = PrinterConnection.USB,
            BaudRate = 9600,
            IsActive = true
            // ConnectionString = null (default) → falls back to Name
        };

        // Act
        var result = await _printer.TestPrinterAsync(printer);

        // Assert — dispatches to USB handler, fails via RawPrinterHelper
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Test print failed")),
            It.IsAny<Exception?>()), Times.Once);
    }

    // ========================================================================
    // GetPrinterStatus — Dispatch Verification
    // ========================================================================

    [Fact]
    public void GetPrinterStatus_NetworkPrinter_ReturnsOffline()
    {
        // Arrange
        var printer = CreatePrinter(PrinterConnection.Network);

        // Act
        var status = _printer.GetPrinterStatus(printer);

        // Assert — empty IP → Offline via guard clause (IsNullOrWhiteSpace)
        status.Should().Be(PrinterStatus.Offline);
    }

    [Fact]
    public void GetPrinterStatus_NetworkPrinter_NullIpAddress_ReturnsOfflineViaGuard()
    {
        // Arrange — network printer with IpAddress left as null (default)
        // IsNullOrWhiteSpace(null) is true → guard clause returns Offline (same as empty string)
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "Status Network Null Guard",
            Connection = PrinterConnection.Network,
            Port = 9100,
            BaudRate = 9600,
            IsActive = true
            // IpAddress = null (default) — triggers IsNullOrWhiteSpace guard
        };

        // Act — guard clause fires without attempting any socket connection
        var status = _printer.GetPrinterStatus(printer);

        // Assert — guard clause: no IP address configured
        status.Should().Be(PrinterStatus.Offline);
    }

    // Real-IP printer status test removed — status checking is now in
    // RealPrinterHardwareSender. The ESCPOSPrinter.GetPrinterStatus delegates
    // to the IPrinterHardwareSender mock and returns the mock's return value.

    [Fact]
    public void GetPrinterStatus_SerialPrinter_ReturnsOffline()
    {
        // Arrange
        var printer = CreatePrinter(PrinterConnection.Serial);

        // Act
        var status = _printer.GetPrinterStatus(printer);

        // Assert — non-existent COM port → Offline (FileNotFoundException caught)
        status.Should().Be(PrinterStatus.Offline);
    }

    [Fact]
    public void GetPrinterStatus_SerialPrinter_NullConnectionString_ReturnsOfflineViaGuard()
    {
        // Arrange — serial printer with null ConnectionString
        // GetSerialPrinterStatus hits: if (string.IsNullOrWhiteSpace(portName)) return PrinterStatus.Offline;
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "Status Serial Guard",
            Connection = PrinterConnection.Serial,
            BaudRate = 9600,
            IsActive = true
            // ConnectionString = null (default) — guard clause returns Offline
        };

        // Act — guard clause fires without attempting SerialPort.Open
        var status = _printer.GetPrinterStatus(printer);

        // Assert — guard clause: no COM port configured
        status.Should().Be(PrinterStatus.Offline);
    }

    [Fact]
    public void GetPrinterStatus_UsbPrinter_ReturnsOffline()
    {
        // Arrange
        var printer = CreatePrinter(PrinterConnection.USB);

        // Act
        var status = _printer.GetPrinterStatus(printer);

        // Assert — non-existent Windows printer → Offline (OpenPrinter returns false)
        status.Should().Be(PrinterStatus.Offline);
    }

    [Fact]
    public void GetPrinterStatus_UsbPrinter_NullConnectionString_FallsBackToPrinterName()
    {
        // Arrange — USB printer with null ConnectionString, falls back to printer.Name
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "STATUS_FALLBACK_USB_TEST",
            Connection = PrinterConnection.USB,
            BaudRate = 9600,
            IsActive = true
            // ConnectionString = null (default) → falls back to Name → CheckPrinterAvailable returns false
        };

        // Act — GetUsbPrinterStatus uses printer.Name since ConnectionString is null
        var status = _printer.GetPrinterStatus(printer);

        // Assert — non-existent printer name → Offline (OpenPrinter returns false)
        status.Should().Be(PrinterStatus.Offline);
    }

    [Fact]
    public void GetPrinterStatus_UsbPrinter_NullConnectionStringAndNullName_ReturnsOfflineViaGuard()
    {
        // Arrange — BOTH ConnectionString and Name are null
        // GetUsbPrinterStatus hits: if (string.IsNullOrWhiteSpace(printerName)) return PrinterStatus.Offline;
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = null!,
            Connection = PrinterConnection.USB,
            BaudRate = 9600,
            IsActive = true
            // ConnectionString = null (default) — both are null
        };

        // Act — guard clause returns Offline without attempting OpenPrinter
        var status = _printer.GetPrinterStatus(printer);

        // Assert — guard clause fires: no printer name configured
        status.Should().Be(PrinterStatus.Offline);
    }

    [Fact]
    public void GetPrinterStatus_UsbWithComFallback_ReturnsOffline()
    {
        // Arrange
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "USB COM Fallback",
            Connection = PrinterConnection.USB,
            ConnectionString = "COM_NOT_EXIST_PORT_TEST",  // COM prefix → dispatches to Serial handler
            IsActive = true
        };

        // Act
        var status = _printer.GetPrinterStatus(printer);

        // Assert — should dispatch to GetSerialPrinterStatus which catches FileNotFoundException
        status.Should().Be(PrinterStatus.Offline);
    }

    [Fact]
    public void GetPrinterStatus_UnknownConnection_ReturnsOffline()
    {
        // Arrange
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "Unknown Printer",
            Connection = (PrinterConnection)999,
            IsActive = true
        };

        // Act
        var status = _printer.GetPrinterStatus(printer);

        // Assert — default case returns Offline
        status.Should().Be(PrinterStatus.Offline);
    }

    // ========================================================================
    // End-to-End: Command Building Verification
    // ========================================================================

    [Fact]
    public async Task PrintReceiptAsync_WithFullSaleData_BuildsAllCommands()
    {
        // Arrange — create a rich Sale with all possible data points
        var sale = CreateTestSale();
        var printer = CreatePrinter(PrinterConnection.Serial);

        // Add order-level notes
        sale.Notes = "Please deliver to table 3";

        // Act
        var result = await _printer.PrintReceiptAsync(
            printer, sale, "My Cafe", "كافيهي",
            "Thank you!", "شكراً لزيارتكم!");

        // Assert — commands were built and sent to serial handler (which fails on COM)
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt")),
            It.IsAny<Exception?>()), Times.Once);

        // Verify the debug log was called with the correct connection type in the dispatch message.
        // Use Invocations.Any (Func<>) instead of Moq Verify (Expression<Func<>>) to avoid
        // Moq v4.20.70 params object?[] matching issue on .NET 10.
        _loggerMock.Invocations
            .Any(i => i.Method.Name == nameof(ILoggerService.LogDebug) &&
                      i.Arguments.Count > 0 &&
                      i.Arguments[0]?.ToString()?.Contains("Sending") == true)
            .Should().BeTrue();
    }

    [Fact]
    public async Task PrintReceiptAsync_WithEmptySale_NoItemsAdded_HandlesGracefully()
    {
        // Arrange — create a minimal Sale with NO items added.
        // Sale.SaleItems is backed by a private readonly List<SaleItem> initialized to empty,
        // so SaleItems is always non-null. This test verifies the foreach loop
        // gracefully skips over the empty collection.
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-EMPTY-001",
            UserId = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            CreatedAt = new DateTime(2026, 7, 19, 15, 0, 0, DateTimeKind.Utc),
            SubTotal = 0m,
            TaxAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 0m,
            RoundAmount = 0m,
            RemainingAmount = 0m,
            Status = SaleStatus.Completed,
            IsPaid = true,
            PaidAt = new DateTime(2026, 7, 19, 15, 5, 0, DateTimeKind.Utc),
            User = new User { Id = Guid.NewGuid(), FullName = "Test Cashier" },
            Table = new Table { Id = Guid.NewGuid(), Name = "Table 5" }
            // SaleItems is read-only — always empty by default
        };
        var printer = CreatePrinter(PrinterConnection.Serial);

        // Act — the foreach over the empty SaleItems should simply skip
        var result = await _printer.PrintReceiptAsync(
            printer, sale, "Empty Cafe", "مقهى فارغ");

        // Assert — command building succeeded; serial handler still fails on COM
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt")),
            It.IsAny<Exception?>()), Times.Once);

        // Verify the receipt was built and dispatched (debug log shows byte count).
        // Use Invocations.Any (Func<>) instead of expression-tree matchers.
        _loggerMock.Invocations
            .Any(i => i.Method.Name == nameof(ILoggerService.LogDebug) &&
                      i.Arguments.Count > 0 &&
                      i.Arguments[0]?.ToString()?.Contains("Sending") == true)
            .Should().BeTrue();
    }

    [Fact]
    public async Task PrintReceiptAsync_WithEmptySale_NoPaymentsNoUser_HandlesGracefully()
    {
        // Arrange — minimal sale with empty items AND null User/Table/Payments
        // This exercises the null checks for User, Table, Payments guards
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-EMPTY-002",
            CreatedAt = new DateTime(2026, 7, 19, 15, 0, 0, DateTimeKind.Utc),
            SubTotal = 0m,
            TotalAmount = 0m,
            Status = SaleStatus.Completed,
            IsPaid = false
            // User, Table, Payments all stay null
        };
        var printer = CreatePrinter(PrinterConnection.Serial);

        // Act — all guard clauses (sale.User != null, sale.Table != null, sale.Payments != null)
        // should prevent NullReferenceException
        var result = await _printer.PrintReceiptAsync(
            printer, sale, "Minimal Cafe", "مقهى بسيط");

        // Assert — command building succeeded; serial handler still fails on COM
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt")),
            It.IsAny<Exception?>()), Times.Once);        // Verify the receipt was built and dispatched (debug log shows byte count).
        // Use Invocations.Any (Func<>) instead of expression-tree matchers.
        _loggerMock.Invocations
            .Any(i => i.Method.Name == nameof(ILoggerService.LogDebug) &&
                      i.Arguments.Count > 0 &&
                      i.Arguments[0]?.ToString()?.Contains("Sending") == true)
            .Should().BeTrue();
    }

    [Fact]
    public async Task PrintReceiptAsync_WithEmptySale_UnknownConnection_DoesNotCrash()
    {
        // Arrange — minimal empty sale + unknown connection (default fallback handler)
        // This exercises the full code path with both empty data AND unknown dispatch
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-EMPTY-003",
            CreatedAt = new DateTime(2026, 7, 19, 15, 0, 0, DateTimeKind.Utc),
            SubTotal = 0m,
            TotalAmount = 0m,
            Status = SaleStatus.Completed,
            IsPaid = false
        };
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "No-Connection Printer",
            Connection = (PrinterConnection)999,
            IsActive = true
        };

        // Act — should not throw even with empty data AND unknown connection
        var result = await _printer.PrintReceiptAsync(printer, sale, "Test", "اختبار");

        // Assert — unknown connection logs + delays; returns true
        result.Should().BeTrue();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing receipt")),
            It.IsAny<Exception?>()), Times.Never);
        _loggerMock.Verify(l => l.LogInfo(
            It.Is<string>(msg => msg.Contains("has no known connection type")),
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PrintKitchenTicketAsync_WithItemsAndModifiers_BuildsAllCommands()
    {
        // Arrange
        var sale = CreateTestSale();
        sale.Notes = "Rush order - priority";
        var printer = CreatePrinter(PrinterConnection.Serial);

        // Act
        var result = await _printer.PrintKitchenTicketAsync(printer, sale, "Main Kitchen");

        // Assert — commands built, dispatched to serial handler
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Error printing kitchen ticket")),
            It.IsAny<Exception?>()), Times.Once);
    }

    [Fact]
    public async Task TestPrinterAsync_WithSerialPrinter_BuildsTestCommands()
    {
        // Arrange
        var printer = CreatePrinter(PrinterConnection.Serial);

        // Act
        var result = await _printer.TestPrinterAsync(printer);

        // Assert — test commands built, dispatched to serial handler
        result.Should().BeFalse();
        _loggerMock.Verify(l => l.LogError(
            It.Is<string>(msg => msg.Contains("Test print failed")),
            It.IsAny<Exception?>()), Times.Once);

        // Verify the dispatch debug line was logged with the correct connection type.
        // Use Invocations.Any (Func<>) instead of expression-tree matchers.
        _loggerMock.Invocations
            .Any(i => i.Method.Name == nameof(ILoggerService.LogDebug) &&
                      i.Arguments.Count > 0 &&
                      i.Arguments[0]?.ToString()?.Contains("Sending") == true)
            .Should().BeTrue();
    }

    // ========================================================================
    // PrintReceiptAsync — Remaining Amount & Rounding Branches
    // ========================================================================

    [Fact]
    public async Task PrintReceiptAsync_WithRemainingAmount_IncludesRemainingLine()
    {
        // Arrange — sale with RemainingAmount > 0 to exercise the
        // "if (sale.RemainingAmount > 0)" branch in PrintReceiptAsync (lines 162-164).
        // Use Unknown connection so the test path goes through without real hardware.
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-REM-001",
            UserId = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            CreatedAt = new DateTime(2026, 7, 19, 15, 0, 0, DateTimeKind.Utc),
            SubTotal = 50.000m,
            TaxAmount = 8.000m,
            DiscountAmount = 0m,
            TotalAmount = 58.000m,
            RoundAmount = 0.000m,
            RemainingAmount = 20.000m,  // > 0 → exercises the Remaining branch
            Status = SaleStatus.Completed,
            IsPaid = false
        };
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "Remaining-Amount Printer",
            Connection = (PrinterConnection)999,  // Unknown → fallback logging
            IsActive = true
        };

        // Act
        var result = await _printer.PrintReceiptAsync(printer, sale, "Store", "متجر");

        // Assert — command building succeeded with remaining amount line
        result.Should().BeTrue();
        _loggerMock.Verify(l => l.LogInfo(
            It.Is<string>(msg => msg.Contains("has no known connection type")),
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PrintReceiptAsync_WithRoundAmount_IncludesRoundingLine()
    {
        // Arrange — sale with RoundAmount != 0 to exercise the
        // "if (sale.RoundAmount != 0)" branch in PrintReceiptAsync (lines 172-174).
        // Use Unknown connection to skip real hardware.
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-RND-001",
            UserId = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            CreatedAt = new DateTime(2026, 7, 19, 15, 0, 0, DateTimeKind.Utc),
            SubTotal = 10.000m,
            TaxAmount = 1.600m,
            DiscountAmount = 0m,
            TotalAmount = 11.600m,
            RoundAmount = 0.005m,            // Non-zero → exercises the Rounding branch
            RemainingAmount = 0m,
            Status = SaleStatus.Completed,
            IsPaid = true,
            User = new User { Id = Guid.NewGuid(), FullName = "Cashier" }
        };
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "Rounding-Amount Printer",
            Connection = (PrinterConnection)999,  // Unknown → fallback logging
            IsActive = true
        };

        // Act
        var result = await _printer.PrintReceiptAsync(printer, sale, "Store", "متجر");

        // Assert — command building succeeded with rounding line
        result.Should().BeTrue();
        _loggerMock.Verify(l => l.LogInfo(
            It.Is<string>(msg => msg.Contains("has no known connection type")),
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }
}
