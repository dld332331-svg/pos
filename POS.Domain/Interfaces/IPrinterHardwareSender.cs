using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Domain.Interfaces;

/// <summary>
/// Abstraction over the low-level printer hardware communication layer.
/// Implementations handle the actual TCP/IP socket, serial port (COM), and
/// Windows Printer API (USB) send/status operations.
///
/// This interface exists so that ESCPOSPrinter can be fully unit-tested
/// by mocking the hardware layer, achieving 100% branch coverage without
/// requiring physical printer hardware.
/// </summary>
public interface IPrinterHardwareSender
{
    /// <summary>
    /// Sends raw ESC/POS commands to a network printer via TCP/IP socket.
    /// </summary>
    Task SendViaNetworkAsync(Printer printer, List<byte[]> commands);

    /// <summary>
    /// Sends raw ESC/POS commands to a serial (COM) port printer.
    /// </summary>
    Task SendViaSerialAsync(Printer printer, List<byte[]> commands);

    /// <summary>
    /// Sends raw ESC/POS commands to a USB-connected printer.
    /// May fall back to a virtual COM port if the connection string starts with "COM".
    /// </summary>
    Task SendViaUsbAsync(Printer printer, List<byte[]> commands);

    /// <summary>
    /// Checks the availability of a network printer by attempting a TCP socket connection.
    /// </summary>
    PrinterStatus GetNetworkPrinterStatus(Printer printer);

    /// <summary>
    /// Checks the availability of a serial (COM) port printer.
    /// </summary>
    PrinterStatus GetSerialPrinterStatus(Printer printer);

    /// <summary>
    /// Checks the availability of a USB printer via the Windows Printer API.
    /// May fall back to serial port check if the connection string starts with "COM".
    /// </summary>
    PrinterStatus GetUsbPrinterStatus(Printer printer);
}
