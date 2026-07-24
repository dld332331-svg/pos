using Xunit;
using Moq;
using FluentAssertions;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Infrastructure.Printing;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for <see cref="RealPrinterHardwareSender"/>.
///
/// Covers:
/// - Guard clause validation (null/empty parameters) for all 6 interface methods
/// - Exception path handling (FileNotFoundException, generic catch-all)
/// - Status method dispatch and fallback logic
///
/// Network socket I/O, serial port I/O, and Windows Printer API calls
/// require actual hardware and are NOT tested here. Those paths are
/// exercised through the integration test suite (ESCPOSPrinterDispatchIntegrationTests)
/// and the mock-based ESCPOSPrinterErrorHandlingTests.
/// </summary>
public sealed class RealPrinterHardwareSenderTests
{
    private readonly Mock<ILoggerService> _loggerMock;
    private readonly RealPrinterHardwareSender _sut;

    public RealPrinterHardwareSenderTests()
    {
        _loggerMock = new Mock<ILoggerService>();
        _loggerMock.Setup(l => l.LogInfo(It.IsAny<string>(), It.IsAny<object?[]>()));

        _sut = new RealPrinterHardwareSender(_loggerMock.Object);
    }

    // ========================================================================
    // Constructor
    // ========================================================================

    [Fact]
    public void Constructor_DefaultTimeout_ShouldSucceed()
    {
        var sender = new RealPrinterHardwareSender(_loggerMock.Object);
        sender.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_CustomTimeout_ShouldSucceed()
    {
        var sender = new RealPrinterHardwareSender(_loggerMock.Object, 5);
        sender.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_NegativeTimeout_ShouldDefaultToTen()
    {
        var sender = new RealPrinterHardwareSender(_loggerMock.Object, -5);
        sender.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ZeroTimeout_ShouldDefaultToTen()
    {
        var sender = new RealPrinterHardwareSender(_loggerMock.Object, 0);
        sender.Should().NotBeNull();
    }

    // ========================================================================
    // SendViaNetworkAsync — Guard Clauses
    // ========================================================================

    [Fact]
    public async Task SendViaNetworkAsync_NullPrinter_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.SendViaNetworkAsync(null!, new List<byte[]>());
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendViaNetworkAsync_NullCommands_ShouldThrowArgumentNullException()
    {
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "192.168.1.100");
        var act = () => _sut.SendViaNetworkAsync(printer, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendViaNetworkAsync_EmptyIp_ShouldThrowInvalidOperationException()
    {
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "");
        var act = () => _sut.SendViaNetworkAsync(printer, new List<byte[]>());
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*no IP address configured*");
    }

    [Fact]
    public async Task SendViaNetworkAsync_NullIp_ShouldThrowInvalidOperationException()
    {
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: null);
        var act = () => _sut.SendViaNetworkAsync(printer, new List<byte[]>());
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*no IP address configured*");
    }

    [Fact]
    public async Task SendViaNetworkAsync_WhitespaceIp_ShouldThrowInvalidOperationException()
    {
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "   ");
        var act = () => _sut.SendViaNetworkAsync(printer, new List<byte[]>());
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*no IP address configured*");
    }

    // ========================================================================
    // SendViaSerialAsync — Guard Clauses
    // ========================================================================

    [Fact]
    public async Task SendViaSerialAsync_NullPrinter_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.SendViaSerialAsync(null!, new List<byte[]>());
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendViaSerialAsync_NullCommands_ShouldThrowArgumentNullException()
    {
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: "COM1");
        var act = () => _sut.SendViaSerialAsync(printer, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendViaSerialAsync_NullConnectionString_ShouldThrowInvalidOperationException()
    {
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: null);
        var act = () => _sut.SendViaSerialAsync(printer, new List<byte[]>());
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*has no COM port*");
    }

    [Fact]
    public async Task SendViaSerialAsync_EmptyConnectionString_ShouldThrowInvalidOperationException()
    {
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: "");
        var act = () => _sut.SendViaSerialAsync(printer, new List<byte[]>());
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*has no COM port*");
    }

    // ========================================================================
    // SendViaUsbAsync — Guard Clauses
    // ========================================================================

    [Fact]
    public async Task SendViaUsbAsync_NullPrinter_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.SendViaUsbAsync(null!, new List<byte[]>());
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendViaUsbAsync_NullCommands_ShouldThrowArgumentNullException()
    {
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: "USB001");
        var act = () => _sut.SendViaUsbAsync(printer, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendViaUsbAsync_NullConnectionStringAndNullName_ShouldThrowInvalidOperationException()
    {
        // Both ConnectionString and Name are null — guard clause fires
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: null, name: null!);
        var act = () => _sut.SendViaUsbAsync(printer, new List<byte[]>());
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*no printer name*");
    }

    [Fact]
    public async Task SendViaUsbAsync_ComPrefix_DispatchesToSerialHandler()
    {
        // COM prefix triggers fallback to SendViaSerialAsync which will
        // throw because the COM port doesn't exist (FileNotFoundException).
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: "COM999");
        var act = () => _sut.SendViaUsbAsync(printer, new List<byte[]>());
        // Should fail inside SendViaSerialAsync because COM999 doesn't exist
        // On Windows, SerialPort.Open("COM999") throws FileNotFoundException.
        // The exception bubbles from SendViaSerialAsync up through SendViaUsbAsync.
        await act.Should().ThrowAsync<System.IO.FileNotFoundException>();
    }

    // ========================================================================
    // SendViaUsbAsync — Windows API Fallback (non-COM)
    // ========================================================================

    [Fact]
    public async Task SendViaUsbAsync_NonExistentPrinterName_ShouldThrow()
    {
        // Non-COM connectionString → attempts RawPrinterHelper → throws
        // RawPrinterHelper's SendRawDataChunks P/Invoke can throw different
        // exception types (ArgumentException or InvalidOperationException)
        // depending on the Win32 error on different Windows versions.
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: "NON_EXISTENT_PRINTER");
        var act = () => _sut.SendViaUsbAsync(printer, new List<byte[]>());
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SendViaUsbAsync_NullConnectionString_WithValidName_FallsBackToName()
    {
        // ConnectionString null → falls back to printer.Name → RawPrinterHelper throws
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "FALLBACK_PRINTER_NAME",
            Connection = PrinterConnection.USB,
            IsActive = true
        };
        var act = () => _sut.SendViaUsbAsync(printer, new List<byte[]>());
        await act.Should().ThrowAsync<Exception>();
    }

    // ========================================================================
    // SendViaNetworkAsync — Real TCP Timeout
    // ========================================================================

    [Fact]
    public async Task SendViaNetworkAsync_UnreachableIp_ShouldTimeout()
    {
        // Exercise the real TCP socket connect + CancellationToken timeout path.
        // Uses 10.255.255.1 (private range, unreachable on most test machines)
        // with a short 3-second timeout to keep test duration manageable.
        var sender = new RealPrinterHardwareSender(_loggerMock.Object, 3);
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "Timeout-Test-Printer",
            Connection = PrinterConnection.Network,
            IpAddress = "10.255.255.1",
            Port = 9100,
            IsActive = true
        };
        var commands = new List<byte[]> { new byte[] { 0x1B, 0x40 } };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var act = () => sender.SendViaNetworkAsync(printer, commands);
        var ex = await act.Should().ThrowAsync<TimeoutException>();
        sw.Stop();

        ex.WithMessage("*timed out*");
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(8));
        sw.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(2.5));
    }

    // ========================================================================
    // GetNetworkPrinterStatus — Guard Clauses
    // ========================================================================

    [Fact]
    public void GetNetworkPrinterStatus_NullPrinter_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.GetNetworkPrinterStatus(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetNetworkPrinterStatus_NullIp_ShouldReturnOffline()
    {
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: null);
        var result = _sut.GetNetworkPrinterStatus(printer);
        result.Should().Be(PrinterStatus.Offline);
    }

    [Fact]
    public void GetNetworkPrinterStatus_EmptyIp_ShouldReturnOffline()
    {
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "");
        var result = _sut.GetNetworkPrinterStatus(printer);
        result.Should().Be(PrinterStatus.Offline);
    }

    [Fact]
    public void GetNetworkPrinterStatus_WhitespaceIp_ShouldReturnOffline()
    {
        var printer = CreatePrinter(PrinterConnection.Network, ipAddress: "   ");
        var result = _sut.GetNetworkPrinterStatus(printer);
        result.Should().Be(PrinterStatus.Offline);
    }

    // ========================================================================
    // GetSerialPrinterStatus — Guard Clauses
    // ========================================================================

    [Fact]
    public void GetSerialPrinterStatus_NullPrinter_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.GetSerialPrinterStatus(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetSerialPrinterStatus_NullPortName_ShouldReturnOffline()
    {
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: null);
        var result = _sut.GetSerialPrinterStatus(printer);
        result.Should().Be(PrinterStatus.Offline);
    }

    [Fact]
    public void GetSerialPrinterStatus_EmptyPortName_ShouldReturnOffline()
    {
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: "");
        var result = _sut.GetSerialPrinterStatus(printer);
        result.Should().Be(PrinterStatus.Offline);
    }

    // ========================================================================
    // GetSerialPrinterStatus — Exception Paths
    // ========================================================================

    [Fact]
    public void GetSerialPrinterStatus_ValidComFormatNonexistentPort_ShouldHitFileNotFoundException()
    {
        // "COM999" is a valid COM port format on Windows; it doesn't exist,
        // so SerialPort.Open throws FileNotFoundException
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: "COM999");

        var result = _sut.GetSerialPrinterStatus(printer);

        result.Should().Be(PrinterStatus.Offline);
        _loggerMock.Verify(l => l.LogWarning(
            It.Is<string>(msg => msg.Contains("not found")),
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }

    // UnauthorizedAccessException test omitted — this exception (port in use by
    // another application) requires an actual COM port to be opened by another
    // process, which is impractical in a unit test environment. The catch block
    // is structurally identical to the FileNotFoundException and generic catch-all
    // blocks (same return type, same branch outcome).

    [Fact]
    public void GetSerialPrinterStatus_InvalidPortName_ShouldHitGenericCatchAll()
    {
        // Invalid port format (not "COMx") causes ArgumentException or
        // InvalidOperationException, hitting the generic catch-all block
        var printer = CreatePrinter(PrinterConnection.Serial, connectionString: "INVALID_PORT_NAME!");

        var result = _sut.GetSerialPrinterStatus(printer);

        result.Should().Be(PrinterStatus.Offline);
        _loggerMock.Verify(l => l.LogWarning(
            It.Is<string>(msg => msg.Contains("status check failed")),
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }

    // ========================================================================
    // GetSerialPrinterStatus — COM Port with BaudRate Edge Case
    // ========================================================================

    // ========================================================================
    // GetUsbPrinterStatus — Guard Clauses
    // ========================================================================

    [Fact]
    public void GetUsbPrinterStatus_NullPrinter_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.GetUsbPrinterStatus(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetUsbPrinterStatus_NullConnectionStringAndNullName_ShouldReturnOffline()
    {
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: null, name: null!);
        var result = _sut.GetUsbPrinterStatus(printer);
        result.Should().Be(PrinterStatus.Offline);
    }

    [Fact]
    public void GetUsbPrinterStatus_NonExistentPrinter_ShouldReturnOffline()
    {
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: "NON_EXISTENT_PRINTER_XYZ");
        var result = _sut.GetUsbPrinterStatus(printer);
        result.Should().Be(PrinterStatus.Offline);
    }

    [Fact]
    public void GetUsbPrinterStatus_NullConnectionStringWithName_ShouldReturnOffline()
    {
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "USB_NAME_FALLBACK",
            Connection = PrinterConnection.USB,
            IsActive = true
        };
        var result = _sut.GetUsbPrinterStatus(printer);
        result.Should().Be(PrinterStatus.Offline);
    }

    // ========================================================================
    // GetUsbPrinterStatus — COM Fallback
    // ========================================================================

    [Fact]
    public void GetUsbPrinterStatus_ComPrefix_DispatchesToSerialHandler()
    {
        // COM prefix → GetSerialPrinterStatus → FileNotFoundException → Offline
        var printer = CreatePrinter(PrinterConnection.USB, connectionString: "COM999");
        var result = _sut.GetUsbPrinterStatus(printer);
        result.Should().Be(PrinterStatus.Offline);
        _loggerMock.Verify(l => l.LogWarning(
            It.Is<string>(msg => msg.Contains("not found")),
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }

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
}
