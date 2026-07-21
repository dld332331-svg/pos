namespace POS.Domain.Enums;

/// <summary>
/// Represents the current operational status of a printer.
/// </summary>
public enum PrinterStatus
{
    /// <summary>Printer is online and ready to print.</summary>
    Online,

    /// <summary>Printer is offline or unreachable.</summary>
    Offline,

    /// <summary>Printer is in an error state (paper out, jam, etc.).</summary>
    Error,

    /// <summary>Printer is currently processing a print job.</summary>
    Printing,

    /// <summary>Printer configuration is unknown or not yet checked.</summary>
    Unknown
}