using System.Runtime.InteropServices;

namespace POS.Infrastructure.Printing;

/// <summary>
/// Sends raw ESC/POS commands to a Windows-installed printer using the
/// Win32 Printer API (winspool.drv). Used for USB-connected printers that
/// are installed as Windows printer devices (e.g., "Generic / Text Only").
/// 
/// Thread-safe: each call opens/closes its own printer handle.
/// </summary>
internal static class RawPrinterHelper
{
    // ============================================================
    // Win32 P/Invoke Declarations
    // ============================================================

    /// <summary>Opens a handle to the specified printer.</summary>
    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    /// <summary>Closes the printer handle.</summary>
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    /// <summary>Notifies the print spooler that a document is being sent.</summary>
    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr StartDocPrinter(IntPtr hPrinter, int level, [In] ref DOC_INFO_1 pDocInfo);

    /// <summary>Notifies the spooler that a new page is starting.</summary>
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    /// <summary>Writes raw bytes to the printer.</summary>
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, [In] byte[] pBytes, int dwCount, out int dwWritten);

    /// <summary>Notifies the spooler that the current page is ending.</summary>
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    /// <summary>Notifies the spooler that the document is complete.</summary>
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    /// <summary>Reads printer status information.</summary>
    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetPrinter(IntPtr hPrinter, int level, IntPtr pPrinter, int cbBuf, out int pcbNeeded);

    // ============================================================
    // Structures
    // ============================================================

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOC_INFO_1
    {
        [MarshalAs(UnmanagedType.LPTStr)] public string pDocName;
        [MarshalAs(UnmanagedType.LPTStr)] public string pOutputFile;
        [MarshalAs(UnmanagedType.LPTStr)] public string pDatatype;
    }

    /// <summary>
    /// PRINTER_INFO_2 used for retrieving printer status.
    /// Only the status field is accessed; other fields are placeholders for layout.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PRINTER_INFO_2
    {
        public IntPtr pServerName;
        public IntPtr pPrinterName;
        public IntPtr pShareName;
        public IntPtr pPortName;
        public IntPtr pDriverName;
        public IntPtr pComment;
        public IntPtr pLocation;
        public IntPtr pDevMode;
        public IntPtr pSepFile;
        public IntPtr pPrintProcessor;
        public IntPtr pDatatype;
        public IntPtr pParameters;
        public IntPtr pSecurityDescriptor;
        public uint Attributes;
        public uint Priority;
        public uint DefaultPriority;
        public uint StartTime;
        public uint UntilTime;
        public uint Status;
        public uint cJobs;
        public uint AveragePPM;
    }

    // ============================================================
    // Printer Status Flags (winspool.h)
    // ============================================================

    private const uint PRINTER_STATUS_PAUSED = 0x00000001;
    private const uint PRINTER_STATUS_ERROR = 0x00000002;
    private const uint PRINTER_STATUS_PENDING_DELETION = 0x00000004;
    private const uint PRINTER_STATUS_PAPER_JAM = 0x00000008;
    private const uint PRINTER_STATUS_PAPER_OUT = 0x00000010;
    private const uint PRINTER_STATUS_MANUAL_FEED = 0x00000020;
    private const uint PRINTER_STATUS_PAPER_PROBLEM = 0x00000040;
    private const uint PRINTER_STATUS_OFFLINE = 0x00000080;
    private const uint PRINTER_STATUS_IO_ACTIVE = 0x00000100;
    private const uint PRINTER_STATUS_BUSY = 0x00000200;
    private const uint PRINTER_STATUS_PRINTING = 0x00000400;
    private const uint PRINTER_STATUS_OUTPUT_BIN_FULL = 0x00000800;
    private const uint PRINTER_STATUS_NOT_AVAILABLE = 0x00001000;
    private const uint PRINTER_STATUS_WAITING = 0x00002000;
    private const uint PRINTER_STATUS_PROCESSING = 0x00004000;
    private const uint PRINTER_STATUS_INITIALIZING = 0x00008000;
    private const uint PRINTER_STATUS_WARMING_UP = 0x00010000;
    private const uint PRINTER_STATUS_TONER_LOW = 0x00020000;
    private const uint PRINTER_STATUS_NO_TONER = 0x00040000;
    private const uint PRINTER_STATUS_PAGE_PUNT = 0x00080000;
    private const uint PRINTER_STATUS_USER_INTERVENTION = 0x00100000;
    private const uint PRINTER_STATUS_OUT_OF_MEMORY = 0x00200000;
    private const uint PRINTER_STATUS_DOOR_OPEN = 0x00400000;
    private const uint PRINTER_STATUS_SERVER_UNKNOWN = 0x00800000;
    private const uint PRINTER_STATUS_POWER_SAVE = 0x01000000;

    // ============================================================
    // Public API
    // ============================================================

    /// <summary>
    /// Sends raw byte data to a Windows-installed printer by name.
    /// The printer must be installed in Windows and accept raw data
    /// (e.g., using the "Generic / Text Only" driver or a manufacturer-specific raw driver).
    /// </summary>
    /// <param name="printerName">Windows printer name (as shown in Printers &amp; Scanners).</param>
    /// <param name="data">Raw ESC/POS command bytes to send.</param>
    /// <param name="docName">Optional document name; defaults to "POS Receipt".</param>
    /// <returns>True if the data was sent successfully.</returns>
    /// <exception cref="InvalidOperationException">If the printer cannot be opened or data cannot be written.</exception>
    public static bool SendRawData(string printerName, byte[] data, string? docName = null)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new ArgumentException("Printer name is required.", nameof(printerName));

        if (data == null || data.Length == 0)
            throw new ArgumentException("Data is required.", nameof(data));

        IntPtr hPrinter = IntPtr.Zero;

        try
        {
            // Open the printer
            if (!OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    $"Failed to open printer '{printerName}'. Win32 error: {errorCode}");
            }

            // Start the document
            var docInfo = new DOC_INFO_1
            {
                pDocName = docName ?? "POS Receipt",
                pOutputFile = null!,
                pDatatype = "RAW"
            };

            var docId = StartDocPrinter(hPrinter, 1, ref docInfo);
            if (docId == IntPtr.Zero)
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    $"Failed to start document on printer '{printerName}'. Win32 error: {errorCode}");
            }

            // Start the page
            if (!StartPagePrinter(hPrinter))
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    $"Failed to start page on printer '{printerName}'. Win32 error: {errorCode}");
            }

            // Write the raw data
            if (!WritePrinter(hPrinter, data, data.Length, out var bytesWritten))
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    $"Failed to write to printer '{printerName}'. Win32 error: {errorCode}");
            }

            if (bytesWritten != data.Length)
            {
                throw new InvalidOperationException(
                    $"Incomplete write to printer '{printerName}': {bytesWritten} of {data.Length} bytes written.");
            }

            // End the page and document
            EndPagePrinter(hPrinter);
            EndDocPrinter(hPrinter);

            return true;
        }
        finally
        {
            if (hPrinter != IntPtr.Zero)
                ClosePrinter(hPrinter);
        }
    }

    /// <summary>
    /// Sends multiple byte arrays as a single raw print job.
    /// All command chunks are concatenated into one document/page.
    /// </summary>
    /// <param name="printerName">Windows printer name.</param>
    /// <param name="commandChunks">List of ESC/POS command byte arrays.</param>
    /// <param name="docName">Optional document name.</param>
    /// <returns>True if all data was sent successfully.</returns>
    public static bool SendRawDataChunks(string printerName, List<byte[]> commandChunks, string? docName = null)
    {
        // Concatenate all chunks into a single buffer for efficient sending
        var totalLength = commandChunks.Sum(c => c.Length);
        var combined = new byte[totalLength];
        var offset = 0;
        foreach (var chunk in commandChunks)
        {
            Buffer.BlockCopy(chunk, 0, combined, offset, chunk.Length);
            offset += chunk.Length;
        }

        return SendRawData(printerName, combined, docName);
    }

    /// <summary>
    /// Checks the status of a Windows-installed printer.
    /// Returns a human-readable status string (e.g., "Online", "Offline", "Paper Out").
    /// </summary>
    /// <param name="printerName">Windows printer name.</param>
    /// <returns>Status description string.</returns>
    public static string GetPrinterStatusString(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            return "Unknown (no name)";

        if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
            return "Offline (cannot open)";

        try
        {
            // First call to get the buffer size needed
            GetPrinter(hPrinter, 2, IntPtr.Zero, 0, out var cbNeeded);
            if (cbNeeded <= 0)
                return "Unknown (no info)";

            var pInfo = Marshal.AllocHGlobal((int)cbNeeded);
            try
            {
                if (!GetPrinter(hPrinter, 2, pInfo, cbNeeded, out _))
                    return "Unknown (query failed)";

                var info = Marshal.PtrToStructure<PRINTER_INFO_2>(pInfo);
                return FormatStatus(info.Status);
            }
            finally
            {
                Marshal.FreeHGlobal(pInfo);
            }
        }
        finally
        {
            ClosePrinter(hPrinter);
        }
    }

    /// <summary>
    /// Attempts to open the printer to verify it exists and is accessible.
    /// </summary>
    /// <param name="printerName">Windows printer name.</param>
    /// <returns>True if the printer handle opens successfully.</returns>
    public static bool CheckPrinterAvailable(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            return false;

        if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
            return false;

        ClosePrinter(hPrinter);
        return true;
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static string FormatStatus(uint status)
    {
        if (status == 0) return "Online";

        var messages = new List<string>();
        if ((status & PRINTER_STATUS_PAUSED) != 0) messages.Add("Paused");
        if ((status & PRINTER_STATUS_ERROR) != 0) messages.Add("Error");
        if ((status & PRINTER_STATUS_PENDING_DELETION) != 0) messages.Add("Pending Deletion");
        if ((status & PRINTER_STATUS_PAPER_JAM) != 0) messages.Add("Paper Jam");
        if ((status & PRINTER_STATUS_PAPER_OUT) != 0) messages.Add("Paper Out");
        if ((status & PRINTER_STATUS_MANUAL_FEED) != 0) messages.Add("Manual Feed");
        if ((status & PRINTER_STATUS_PAPER_PROBLEM) != 0) messages.Add("Paper Problem");
        if ((status & PRINTER_STATUS_OFFLINE) != 0) messages.Add("Offline");
        if ((status & PRINTER_STATUS_IO_ACTIVE) != 0) messages.Add("I/O Active");
        if ((status & PRINTER_STATUS_BUSY) != 0) messages.Add("Busy");
        if ((status & PRINTER_STATUS_PRINTING) != 0) messages.Add("Printing");
        if ((status & PRINTER_STATUS_OUTPUT_BIN_FULL) != 0) messages.Add("Output Bin Full");
        if ((status & PRINTER_STATUS_NOT_AVAILABLE) != 0) messages.Add("Not Available");
        if ((status & PRINTER_STATUS_WAITING) != 0) messages.Add("Waiting");
        if ((status & PRINTER_STATUS_PROCESSING) != 0) messages.Add("Processing");
        if ((status & PRINTER_STATUS_INITIALIZING) != 0) messages.Add("Initializing");
        if ((status & PRINTER_STATUS_WARMING_UP) != 0) messages.Add("Warming Up");
        if ((status & PRINTER_STATUS_TONER_LOW) != 0) messages.Add("Toner Low");
        if ((status & PRINTER_STATUS_NO_TONER) != 0) messages.Add("No Toner");
        if ((status & PRINTER_STATUS_PAGE_PUNT) != 0) messages.Add("Page Punt");
        if ((status & PRINTER_STATUS_USER_INTERVENTION) != 0) messages.Add("User Intervention");
        if ((status & PRINTER_STATUS_OUT_OF_MEMORY) != 0) messages.Add("Out of Memory");
        if ((status & PRINTER_STATUS_DOOR_OPEN) != 0) messages.Add("Door Open");
        if ((status & PRINTER_STATUS_SERVER_UNKNOWN) != 0) messages.Add("Server Unknown");
        if ((status & PRINTER_STATUS_POWER_SAVE) != 0) messages.Add("Power Save");

        return messages.Count > 0 ? string.Join(", ", messages) : $"Unknown (0x{status:X8})";
    }
}
