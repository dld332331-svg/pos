using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Printing;

/// <summary>
/// Real implementation of <see cref="IPrinterHardwareSender"/> that communicates
/// with actual printer hardware via TCP/IP sockets, serial (COM) ports, and
/// the Windows Printer API (winspool.drv).
///
/// This code was extracted from the private methods of <see cref="ESCPOSPrinter"/>
/// to enable mocking of the hardware layer for comprehensive unit testing.
/// </summary>
public sealed class RealPrinterHardwareSender : IPrinterHardwareSender
{
    private readonly ILoggerService _logger;
    private readonly int _connectTimeoutSeconds;

    /// <summary>
    /// Initializes a new instance of <see cref="RealPrinterHardwareSender"/>.
    /// </summary>
    /// <param name="logger">Logger service for audit and debug traces.</param>
    /// <param name="connectTimeoutSeconds">
    /// TCP socket connect timeout in seconds (default 10).
    /// Used only for network printer connections.
    /// </param>
    public RealPrinterHardwareSender(ILoggerService logger, int connectTimeoutSeconds = 10)
    {
        _logger = logger;
        _connectTimeoutSeconds = connectTimeoutSeconds > 0 ? connectTimeoutSeconds : 10;
    }

    // ============================================================
    // Network (TCP/IP)
    // ============================================================

    /// <inheritdoc />
    public async Task SendViaNetworkAsync(Printer printer, List<byte[]> commands)
    {
        ArgumentNullException.ThrowIfNull(printer);
        ArgumentNullException.ThrowIfNull(commands);

        if (string.IsNullOrWhiteSpace(printer.IpAddress))
        {
            throw new InvalidOperationException(
                $"Network printer '{printer.Name}' has no IP address configured.");
        }

        var port = printer.Port > 0 ? printer.Port : 9100;
        var address = System.Net.IPAddress.Parse(printer.IpAddress);
        var endpoint = new System.Net.IPEndPoint(address, port);

        using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        socket.NoDelay = true;
        socket.ReceiveTimeout = 5000;
        socket.SendTimeout = 5000;

        // Connect with configurable cancellation support
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_connectTimeoutSeconds));
        try
        {
            await socket.ConnectAsync(endpoint, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"Connection to printer '{printer.Name}' at {printer.IpAddress}:{port} timed out after {_connectTimeoutSeconds} seconds.");
        }

        if (!socket.Connected)
        {
            throw new InvalidOperationException(
                $"Failed to connect to printer '{printer.Name}' at {printer.IpAddress}:{port}.");
        }

        // Write each command chunk sequentially using the network stream
        using var networkStream = new NetworkStream(socket, ownsSocket: false);
        foreach (var cmd in commands)
        {
            await networkStream.WriteAsync(cmd.AsMemory(0, cmd.Length)).ConfigureAwait(false);
        }

        await networkStream.FlushAsync().ConfigureAwait(false);

        _logger.LogDebug(
            "Successfully sent {CommandCount} commands ({TotalBytes} bytes) to network printer {PrinterName} at {Ip}:{Port}",
            commands.Count, commands.Sum(c => c.Length), printer.Name, printer.IpAddress, port);
    }

    // ============================================================
    // Serial (COM Port)
    // ============================================================

    /// <inheritdoc />
    public async Task SendViaSerialAsync(Printer printer, List<byte[]> commands)
    {
        ArgumentNullException.ThrowIfNull(printer);
        ArgumentNullException.ThrowIfNull(commands);

        var portName = printer.ConnectionString;
        if (string.IsNullOrWhiteSpace(portName))
        {
            throw new InvalidOperationException(
                $"Serial printer '{printer.Name}' has no COM port configured in ConnectionString.");
        }

        var baudRate = printer.BaudRate > 0 ? printer.BaudRate : 9600;
        using var serialPort = new SerialPort(portName)
        {
            BaudRate = baudRate,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            WriteTimeout = 5000,
            ReadTimeout = 5000,
            Encoding = Encoding.UTF8
        };

        // Open the port
        serialPort.Open();

        // Write each command chunk asynchronously via the base stream
        var baseStream = serialPort.BaseStream;
        foreach (var cmd in commands)
        {
            await baseStream.WriteAsync(cmd.AsMemory(0, cmd.Length)).ConfigureAwait(false);
        }

        await baseStream.FlushAsync().ConfigureAwait(false);

        _logger.LogDebug(
            "Successfully sent {CommandCount} commands ({TotalBytes} bytes) to serial printer {PrinterName} on {Port}",
            commands.Count, commands.Sum(c => c.Length), printer.Name, portName);
    }

    // ============================================================
    // USB (Windows Printer API / Virtual COM)
    // ============================================================

    /// <inheritdoc />
    public async Task SendViaUsbAsync(Printer printer, List<byte[]> commands)
    {
        ArgumentNullException.ThrowIfNull(printer);
        ArgumentNullException.ThrowIfNull(commands);

        var connectionString = printer.ConnectionString;

        // Method 1: Virtual COM port (e.g., USB CDC ACM)
        if (!string.IsNullOrWhiteSpace(connectionString) &&
            connectionString.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
        {
            await SendViaSerialAsync(printer, commands);
            return;
        }

        // Method 2: Windows Printer API — send raw data to installed printer
        var printerName = !string.IsNullOrWhiteSpace(connectionString)
            ? connectionString
            : printer.Name;

        if (string.IsNullOrWhiteSpace(printerName))
        {
            throw new InvalidOperationException(
                $"USB printer '{printer.Name}' has no printer name or ConnectionString configured.");
        }

        // Use RawPrinterHelper's concatenation helper
        var success = RawPrinterHelper.SendRawDataChunks(printerName, commands, "POS Receipt");

        if (!success)
        {
            throw new InvalidOperationException(
                $"Failed to send data to USB printer '{printerName}' via Windows Printer API.");
        }

        _logger.LogDebug(
            "Successfully sent {CommandCount} commands ({TotalBytes} bytes) to USB printer {PrinterName} via Windows API (target: {TargetName})",
            commands.Count, commands.Sum(c => c.Length), printer.Name, printerName);
    }

    // ============================================================
    // Status Checks
    // ============================================================

    /// <inheritdoc />
    public PrinterStatus GetNetworkPrinterStatus(Printer printer)
    {
        ArgumentNullException.ThrowIfNull(printer);

        if (string.IsNullOrWhiteSpace(printer.IpAddress))
            return PrinterStatus.Offline;

        var port = printer.Port > 0 ? printer.Port : 9100;

        try
        {
            using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.NoDelay = true;

            var result = Task.Run(async () =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try
                {
                    var endpoint = new System.Net.IPEndPoint(
                        System.Net.IPAddress.Parse(printer.IpAddress), port);
                    await socket.ConnectAsync(endpoint, cts.Token).ConfigureAwait(false);
                    return socket.Connected;
                }
                catch
                {
                    return false;
                }
            }).GetAwaiter().GetResult();

            if (result)
            {
                _logger.LogInfo("Network printer {PrinterName} at {Ip}:{Port} is online",
                    printer.Name, printer.IpAddress, port);
                return PrinterStatus.Online;
            }

            _logger.LogWarning("Network printer {PrinterName} at {Ip}:{Port} is offline (timeout)",
                printer.Name, printer.IpAddress, port);
            return PrinterStatus.Offline;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Network printer {PrinterName} status check failed: {Message}",
                printer.Name, ex.Message);
            return PrinterStatus.Offline;
        }
    }

    /// <inheritdoc />
    public PrinterStatus GetSerialPrinterStatus(Printer printer)
    {
        ArgumentNullException.ThrowIfNull(printer);

        var portName = printer.ConnectionString;
        if (string.IsNullOrWhiteSpace(portName))
            return PrinterStatus.Offline;

        try
        {
            using var serialPort = new SerialPort(portName);
            serialPort.Open();
            serialPort.Close();

            _logger.LogInfo("Serial printer {PrinterName} on {Port} is online",
                printer.Name, portName);
            return PrinterStatus.Online;
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning("Serial port {Port} not found for printer {PrinterName}",
                portName, printer.Name);
            return PrinterStatus.Offline;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Serial port {Port} for printer {PrinterName} is in use by another application",
                portName, printer.Name);
            return PrinterStatus.Error;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Serial printer {PrinterName} status check failed: {Message}",
                printer.Name, ex.Message);
            return PrinterStatus.Offline;
        }
    }

    /// <inheritdoc />
    public PrinterStatus GetUsbPrinterStatus(Printer printer)
    {
        ArgumentNullException.ThrowIfNull(printer);

        var connectionString = printer.ConnectionString;

        // Virtual COM port fallback
        if (!string.IsNullOrWhiteSpace(connectionString) &&
            connectionString.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
        {
            return GetSerialPrinterStatus(printer);
        }

        var printerName = !string.IsNullOrWhiteSpace(connectionString)
            ? connectionString
            : printer.Name;

        if (string.IsNullOrWhiteSpace(printerName))
        {
            return PrinterStatus.Offline;
        }

        if (RawPrinterHelper.CheckPrinterAvailable(printerName))
        {
            _logger.LogInfo("USB printer {PrinterName} is available via Windows API", printerName);
            return PrinterStatus.Online;
        }

        _logger.LogWarning("USB printer {PrinterName} is not available via Windows API", printerName);
        return PrinterStatus.Offline;
    }
}
