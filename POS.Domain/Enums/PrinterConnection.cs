namespace POS.Domain.Enums;

/// <summary>
/// Specifies how a printer connects to the POS system.
/// </summary>
public enum PrinterConnection
{
    /// <summary>Printer connected via USB cable.</summary>
    USB,

    /// <summary>Printer connected over a network (Ethernet/Wi-Fi).</summary>
    Network,

    /// <summary>Printer connected via serial port (COM).</summary>
    Serial
}