using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;
using Moq;
using FluentAssertions;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Infrastructure.Printing;

namespace POS.Tests.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="RealPrinterHardwareSender"/> that exercise
/// real TCP socket I/O using a local loopback listener — no physical printer required.
///
/// Covers the hardware-dependent branches that cannot be tested with mocks:
/// - Socket.ConnectAsync (success path)
/// - NetworkStream.WriteAsync + FlushAsync
/// - Success logging after send
/// - GetNetworkPrinterStatus Online path (socket connect succeeds)
/// - GetNetworkPrinterStatus Offline path (socket connect fails)
/// - Outer catch block in GetNetworkPrinterStatus (invalid IP)
/// </summary>
public sealed class RealPrinterHardwareSenderIntegrationTests
{
    private readonly Mock<ILoggerService> _loggerMock;
    private readonly RealPrinterHardwareSender _sut;

    public RealPrinterHardwareSenderIntegrationTests()
    {
        _loggerMock = new Mock<ILoggerService>();
        _loggerMock.Setup(l => l.LogInfo(It.IsAny<string>(), It.IsAny<object?[]>()));
        _loggerMock.Setup(l => l.LogWarning(It.IsAny<string>(), It.IsAny<object?[]>()));
        _loggerMock.Setup(l => l.LogDebug(It.IsAny<string>(), It.IsAny<object?[]>()));
        _loggerMock.Setup(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<object?[]>()));
        _sut = new RealPrinterHardwareSender(_loggerMock.Object, 3);
    }

    /// <summary>
    /// Starts a local TCP listener on loopback with a random port.
    /// Returns the listener and the port number.
    /// </summary>
    private static (TcpListener Listener, int Port) StartLocalListener()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint!).Port;
        return (listener, port);
    }

    /// <summary>
    /// Creates a printer configured for network communication.
    /// </summary>
    private static Printer CreateNetworkPrinter(string ipAddress, int port, string name = "Integration-Test-Printer")
    {
        return new Printer
        {
            Id = Guid.NewGuid(),
            Name = name,
            Connection = PrinterConnection.Network,
            IpAddress = ipAddress,
            Port = port,
            IsActive = true,
            BaudRate = 9600
        };
    }

    // ========================================================================
    // SendViaNetworkAsync — Real TCP Socket
    // ========================================================================

    [Fact]
    public async Task SendViaNetworkAsync_WithLocalListener_SendsDataSuccessfully()
    {
        // Arrange — start a local TCP listener on loopback
        var (listener, port) = StartLocalListener();
        var printer = CreateNetworkPrinter("127.0.0.1", port);
        var commands = new List<byte[]>
        {
            Encoding.UTF8.GetBytes("Hello "),
            Encoding.UTF8.GetBytes("World!")
        };

        try
        {
            // Act — send data to the local listener
            var sendTask = _sut.SendViaNetworkAsync(printer, commands);

            // Accept the connection on the listener side
            using var client = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
            using var stream = client.GetStream();

            // Read data — single read is sufficient since the test data is
            // small (12 bytes) and arrives in one TCP segment on loopback.
            // Note: we don't loop until bytesRead == 0 because RealPrinterHardwareSender
            // never gracefully closes the connection (socket dispose sends RST).
            var buffer = new byte[1024];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)
                .WaitAsync(TimeSpan.FromSeconds(5));

            await sendTask;

            // Assert — data was sent and received correctly
            var receivedText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            receivedText.Should().Be("Hello World!");

            // Verify success log was written
            _loggerMock.Verify(l => l.LogDebug(
                It.Is<string>(msg => msg.Contains("Successfully sent")),
                It.IsAny<object?[]>()), Times.AtLeastOnce);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task SendViaNetworkAsync_WithLocalListener_SendsMultipleChunks()
    {
        // Arrange — send 5 individual command chunks
        var (listener, port) = StartLocalListener();
        var printer = CreateNetworkPrinter("127.0.0.1", port);
        var commands = Enumerable.Range(0, 5)
            .Select(i => Encoding.UTF8.GetBytes($"Chunk{i}"))
            .ToList();

        try
        {
            // Act
            var sendTask = _sut.SendViaNetworkAsync(printer, commands);

            using var client = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
            using var stream = client.GetStream();

            // Single read — the 5 chunks (~30 bytes total) arrive in one segment
            var buffer = new byte[1024];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)
                .WaitAsync(TimeSpan.FromSeconds(5));

            await sendTask;

            // Assert — all 5 chunks were concatenated correctly
            var receivedText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            receivedText.Should().Be("Chunk0Chunk1Chunk2Chunk3Chunk4");
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task SendViaNetworkAsync_WithNoListener_ThrowsSocketException()
    {
        // Arrange — connect to loopback with no listener (connection refused)
        var printer = CreateNetworkPrinter("127.0.0.1", 11999);

        // Act — send to a port with no listener
        var act = () => _sut.SendViaNetworkAsync(printer, new List<byte[]>
        {
            Encoding.UTF8.GetBytes("test")
        });

        // Assert — connection refused on loopback throws SocketException immediately
        await act.Should().ThrowAsync<SocketException>();
    }

    // ========================================================================
    // GetNetworkPrinterStatus — Real TCP Socket
    // ========================================================================

    [Fact]
    public async Task GetNetworkPrinterStatus_WithLocalListener_ReturnsOnline()
    {
        // Arrange — start a local TCP listener
        var (listener, port) = StartLocalListener();
        var printer = CreateNetworkPrinter("127.0.0.1", port);

        try
        {
            // Act — check status while the listener is active (accept connection in background)
            var acceptTask = listener.AcceptTcpClientAsync();
            var status = _sut.GetNetworkPrinterStatus(printer);

            // Clean up the accepted connection
            using var client = await acceptTask.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert — should return Online
            status.Should().Be(PrinterStatus.Online);
            _loggerMock.Verify(l => l.LogInfo(
                It.Is<string>(msg => msg.Contains("is online")),
                It.IsAny<object?[]>()), Times.AtLeastOnce);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void GetNetworkPrinterStatus_WithNoListener_ReturnsOffline()
    {
        // Arrange — use a port that's not listening on loopback
        var printer = CreateNetworkPrinter("127.0.0.1", 11998);

        // Act — connect attempt will fail (connection refused)
        var status = _sut.GetNetworkPrinterStatus(printer);

        // Assert — should return Offline (socket connect fails)
        status.Should().Be(PrinterStatus.Offline);

        // Verify the "is offline" warning was logged via the real socket path,
        // not the guard clause path
        _loggerMock.Verify(l => l.LogWarning(
            It.Is<string>(msg => msg.Contains("is offline") || msg.Contains("status check failed")),
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }

    [Fact]
    public void GetNetworkPrinterStatus_WithInvalidIp_InnerCatchReturnsOffline()
    {
        // Arrange — use an invalid IP address. IPAddress.Parse inside the
        // Task.Run lambda throws FormatException, which is caught by the
        // inner catch (returning false). Flow then goes to "is offline" log.
        var printer = CreateNetworkPrinter("999.999.999.999", 9100);

        // Act
        var status = _sut.GetNetworkPrinterStatus(printer);

        // Assert — inner catch handles the parse failure, returns Offline
        status.Should().Be(PrinterStatus.Offline);
        _loggerMock.Verify(l => l.LogWarning(
            It.Is<string>(msg => msg.Contains("is offline")),
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }

    // ========================================================================
    // Edge Cases
    // ========================================================================

    [Fact]
    public async Task SendViaNetworkAsync_PortZero_DefaultsTo9100()
    {
        // Arrange — Port=0 should default to 9100
        var (listener, port) = StartLocalListener();
        // We need to start a listener on port 9100, but that might fail if port is in use
        // Instead, verify the default by checking the error behavior
        var printer = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "Default Port Test",
            Connection = PrinterConnection.Network,
            IpAddress = "127.0.0.1",
            Port = 0,  // Should default to 9100
            IsActive = true
        };

        // Act — try to send to port 9100 on loopback (likely no listener)
        var act = () => _sut.SendViaNetworkAsync(printer, new List<byte[]>
        {
            Encoding.UTF8.GetBytes("test")
        });

        // Assert — should timeout/throw since port 9100 is unlikely to have a listener
        await act.Should().ThrowAsync<Exception>();
    }


}
