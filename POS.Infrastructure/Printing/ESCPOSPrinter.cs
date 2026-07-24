using System.Text;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Printing;

/// <summary>
/// ESC/POS thermal printer service implementation.
/// Supports receipt printing with Arabic text (UTF-8) and kitchen ticket printing.
/// 
/// Hardware communication (Section 11.4):
/// - Network (TCP/IP): TcpClient on IP:Port (default 9100)
/// - Serial (COM): SerialPort via ConnectionString (e.g., "COM1")
/// - USB: RawPrinterHelper → Windows Printer API (winspool.drv)
///   Falls back to virtual COM port if ConnectionString starts with "COM".
/// </summary>
public class ESCPOSPrinter : IPrinterService
{
    private readonly ILoggerService _logger;
    private readonly IPrinterHardwareSender _hardwareSender;

    // ESC/POS Control Characters
    private const byte ESC = 0x1B;
    private const byte GS = 0x1D;
    private const byte LF = 0x0A;

    /// <summary>
    /// Initializes a new instance of <see cref="ESCPOSPrinter"/>.
    /// </summary>
    /// <param name="logger">Logger service for audit and debug traces.</param>
    /// <param name="hardwareSender">
    /// Hardware-level printer communication service.
    /// Inject a mock for unit testing; use <see cref="RealPrinterHardwareSender"/> in production.
    /// </param>
    public ESCPOSPrinter(ILoggerService logger, IPrinterHardwareSender hardwareSender)
    {
        _logger = logger;
        _hardwareSender = hardwareSender;
    }

    public async Task<bool> PrintReceiptAsync(Printer printer, Sale sale, string storeName, string storeNameArabic, string footerMessage = "", string footerMessageArabic = "")
    {
        ArgumentNullException.ThrowIfNull(printer);
        ArgumentNullException.ThrowIfNull(sale);
        try
        {
            var commands = new List<byte[]>();

            // Initialize printer
            commands.Add(new byte[] { ESC, 0x40 }); // ESC @ - Initialize

            // Set UTF-8 encoding for Arabic support
            commands.Add(new byte[] { ESC, 0x74, 0x10 }); // ESC t 16 - Select character table (UTF-8)

            // Store name (centered, double width)
            commands.Add(BuildCenterText(storeName, true, true));
            if (!string.IsNullOrEmpty(storeNameArabic))
            {
                commands.Add(BuildCenterText(storeNameArabic, true, false));
            }

            // Separator line
            commands.Add(BuildSeparatorLine('='));

            // Date and invoice info
            var dateStr = sale.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            var invoiceInfo = $"Date: {dateStr}";
            if (!string.IsNullOrEmpty(sale.InvoiceNumber))
                invoiceInfo += $"  Invoice: {sale.InvoiceNumber}";
            commands.Add(BuildLine(invoiceInfo));

            // Cashier
            if (sale.User != null)
            {
                commands.Add(BuildLine($"Cashier: {sale.User.FullName}"));
            }

            // Table info
            if (sale.Table != null)
            {
                var tableName = string.IsNullOrEmpty(sale.Table.ArabicName)
                    ? sale.Table.Name
                    : $"{sale.Table.Name} / {sale.Table.ArabicName}";
                commands.Add(BuildLine($"Table: {tableName}"));
            }

            // Customer
            if (sale.Customer != null)
            {
                commands.Add(BuildLine($"Customer: {sale.Customer.Name}"));
            }

            commands.Add(BuildSeparatorLine('-'));

            // Column headers
            commands.Add(BuildLine("Item                    Qty   Price     Total"));
            commands.Add(BuildSeparatorLine('-'));

            // Sale items
            foreach (var item in sale.SaleItems ?? new List<SaleItem>())
            {
                var name = string.IsNullOrEmpty(item.ProductArabicName)
                    ? item.ProductName
                    : item.ProductName;

                commands.Add(BuildItemLine(name, item.Quantity, item.UnitPrice, item.TotalPrice));

                // Arabic name on second line if present
                if (!string.IsNullOrEmpty(item.ProductArabicName))
                {
                    commands.Add(BuildLine($"  {item.ProductArabicName}"));
                }

                // Modifiers
                if (item.Modifiers != null)
                {
                    foreach (var mod in item.Modifiers)
                    {
                        var modName = string.IsNullOrEmpty(mod.ModifierArabicName)
                            ? mod.ModifierName
                            : $"{mod.ModifierName}";
                        var sizePrefix = !string.IsNullOrEmpty(mod.SizeName) ? $"[{mod.SizeName}] " : "";
                        commands.Add(BuildLine($"    + {sizePrefix}{modName}"));
                    }
                }
            }

            commands.Add(BuildSeparatorLine('-'));

            // Totals
            commands.Add(BuildRightText($"Subtotal: {sale.SubTotal:N3}"));
            if (sale.TaxAmount > 0)
            {
                commands.Add(BuildRightText($"Tax:      {sale.TaxAmount:N3}"));
            }
            if (sale.DiscountAmount > 0)
            {
                commands.Add(BuildRightText($"Discount: -{sale.DiscountAmount:N3}"));
            }
            commands.Add(BuildSeparatorLine('='));
            commands.Add(BuildRightText($"TOTAL:    {sale.TotalAmount:N3}", true, true));
            commands.Add(BuildSeparatorLine('='));

            // Payments
            if (sale.Payments != null)
            {
                foreach (var payment in sale.Payments)
                {
                    var method = payment.PaymentMethod.ToString();
                    var refInfo = !string.IsNullOrEmpty(payment.ReferenceNumber)
                        ? $" (Ref: {payment.ReferenceNumber})"
                        : "";
                    var tipInfo = payment.TipAmount > 0 ? $" + Tip: {payment.TipAmount:N3}" : "";
                    commands.Add(BuildLine($"Paid ({method}{refInfo}): {payment.Amount:N3}{tipInfo}"));
                }
            }

            if (sale.RemainingAmount > 0)
            {
                commands.Add(BuildRightText($"Remaining: {sale.RemainingAmount:N3}"));
            }
            else if (sale.Payments != null && sale.Payments.Any())
            {
                commands.Add(BuildRightText($"Change:    {sale.Payments.Sum(p => p.Amount) - sale.TotalAmount:N3}"));
            }

            // Round amount
            if (sale.RoundAmount != 0)
            {
                commands.Add(BuildRightText($"Rounding:  {sale.RoundAmount:N3}"));
            }

            commands.Add(BuildSeparatorLine('-'));

            // Footer
            commands.Add(BuildCenterText(footerMessage));
            if (!string.IsNullOrEmpty(footerMessageArabic))
            {
                commands.Add(BuildCenterText(footerMessageArabic));
            }

            commands.Add(new byte[] { LF, LF, LF }); // Feed lines
            commands.Add(new byte[] { ESC, 0x6D, 0x01 }); // ESC m 1 - Partial cut (or full cut with 0x00)

            // Barcode (Code128)
            if (!string.IsNullOrEmpty(sale.InvoiceNumber))
            {
                commands.Add(BuildBarcode(sale.InvoiceNumber));
                commands.Add(new byte[] { LF });
            }

            // Send to printer
            await SendToPrinterAsync(printer, commands);

            _logger.LogInfo("Receipt printed successfully for sale {SaleId}", sale.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error printing receipt for sale {sale.Id}: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<bool> PrintKitchenTicketAsync(Printer printer, Sale sale, string kitchenStationName)
    {
        ArgumentNullException.ThrowIfNull(printer);
        ArgumentNullException.ThrowIfNull(sale);
        try
        {
            var commands = new List<byte[]>();

            // Initialize printer
            commands.Add(new byte[] { ESC, 0x40 }); // ESC @
            commands.Add(new byte[] { ESC, 0x74, 0x10 }); // UTF-8

            // Kitchen ticket header
            commands.Add(BuildCenterText("** KITCHEN ORDER **", true, true));
            commands.Add(BuildSeparatorLine('*'));

            // Order info
            var orderNumber = string.IsNullOrEmpty(sale.InvoiceNumber)
                ? $"#{sale.Id.ToString()[..8]}"
                : sale.InvoiceNumber;
            commands.Add(BuildLine($"Order #: {orderNumber}"));
            commands.Add(BuildLine($"Time:   {sale.CreatedAt:HH:mm:ss}"));
            commands.Add(BuildLine($"Station: {kitchenStationName}"));

            if (sale.Table != null)
            {
                commands.Add(BuildLine($"Table:  {sale.Table.Name}"));
            }

            // Cashier
            if (sale.User != null)
            {
                commands.Add(BuildLine($"Cashier: {sale.User.FullName}"));
            }

            commands.Add(BuildSeparatorLine('-'));

            // Items with modifiers
            int itemIndex = 0;
            foreach (var item in sale.SaleItems ?? new List<SaleItem>())
            {
                itemIndex++;
                var qty = item.Quantity;
                var name = item.ProductName;

                // Print with quantity
                commands.Add(BuildLine($"[{itemIndex}] x{qty} {name}"));

                // Arabic name if available
                if (!string.IsNullOrEmpty(item.ProductArabicName))
                {
                    commands.Add(BuildLine($"       {item.ProductArabicName}"));
                }

                // Modifiers
                if (item.Modifiers != null)
                {
                    foreach (var mod in item.Modifiers)
                    {
                        var sizeInfo = !string.IsNullOrEmpty(mod.SizeName) ? $" ({mod.SizeName})" : "";
                        var modStr = $"    -> {mod.ModifierName}{sizeInfo}";
                        if (!string.IsNullOrEmpty(mod.ModifierArabicName))
                        {
                            modStr += $" / {mod.ModifierArabicName}";
                        }
                        commands.Add(BuildLine(modStr));
                    }
                }

                // Notes
                if (!string.IsNullOrEmpty(item.Notes))
                {
                    commands.Add(BuildLine($"    NOTE: {item.Notes}"));
                }

                commands.Add(BuildLine("")); // Blank line between items
            }

            // Order-level notes
            if (!string.IsNullOrEmpty(sale.Notes))
            {
                commands.Add(BuildSeparatorLine('-'));
                commands.Add(BuildCenterText($"ORDER NOTES: {sale.Notes}", true));
            }

            commands.Add(BuildSeparatorLine('*'));
            commands.Add(new byte[] { LF, LF, LF });
            commands.Add(new byte[] { GS, 0x56, 0x00 }); // GS V 0 - Full cut

            await SendToPrinterAsync(printer, commands);

            _logger.LogInfo("Kitchen ticket printed for sale {SaleId} at station {Station}", sale.Id, kitchenStationName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error printing kitchen ticket for sale {sale.Id}: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<bool> TestPrinterAsync(Printer printer)
    {
        ArgumentNullException.ThrowIfNull(printer);
        try
        {
            var commands = new List<byte[]>
            {
                new byte[] { ESC, 0x40 }, // Initialize
                new byte[] { ESC, 0x74, 0x10 }, // UTF-8
            };

            commands.Add(BuildCenterText("*** PRINTER TEST ***", true, true));
            commands.Add(BuildSeparatorLine('='));
            commands.Add(BuildLine("Printer Name: " + (printer.Name ?? "N/A")));
            commands.Add(BuildLine("Connection: " + (printer.IpAddress ?? "N/A")));
            commands.Add(BuildLine("Port: " + printer.Port));
            commands.Add(BuildLine($"Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"));
            commands.Add(BuildSeparatorLine('='));
            commands.Add(BuildCenterText("Arabic Test: مرحباً بالعالم"));
            commands.Add(BuildCenterText("Numbers: 0123456789"));
            commands.Add(BuildCenterText("Special: @#$%^&*()"));
            commands.Add(BuildSeparatorLine('='));
            commands.Add(BuildCenterText("Test PASSED", true));
            commands.Add(new byte[] { LF, LF });
            commands.Add(new byte[] { GS, 0x56, 0x00 }); // Full cut

            await SendToPrinterAsync(printer, commands);
            _logger.LogInfo("Test print sent to printer {PrinterId}", printer.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Test print failed for printer {printer.Id}: {ex.Message}", ex);
            return false;
        }
    }

    public PrinterStatus GetPrinterStatus(Printer printer)
    {
        ArgumentNullException.ThrowIfNull(printer);
        try
        {
            switch (printer.Connection)
            {
                case PrinterConnection.Network:
                    return _hardwareSender.GetNetworkPrinterStatus(printer);

                case PrinterConnection.Serial:
                    return _hardwareSender.GetSerialPrinterStatus(printer);

                case PrinterConnection.USB:
                    return _hardwareSender.GetUsbPrinterStatus(printer);

                default:
                    _logger.LogWarning("Unknown printer connection type {Connection} for printer {PrinterName}",
                        printer.Connection, printer.Name);
                    return PrinterStatus.Offline;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error checking status for printer {printer.Name}: {ex.Message}", ex);
            return PrinterStatus.Offline;
        }
    }

    // ============================================================
    // ESC/POS Command Builders
    // ============================================================

    private static byte[] BuildCenterText(string text, bool bold = false, bool doubleSize = false)
    {
        var result = new List<byte>();

        // Select justification: center
        result.AddRange(new byte[] { ESC, 0x61, 0x01 });

        // Bold
        if (bold)
            result.AddRange(new byte[] { ESC, 0x45, 0x01 });

        // Double size
        if (doubleSize)
            result.AddRange(new byte[] { GS, 0x21, 0x11 }); // Both width and height

        // Text in UTF-8
        result.AddRange(Encoding.UTF8.GetBytes(text));
        result.Add(LF);

        // Reset bold
        if (bold)
            result.AddRange(new byte[] { ESC, 0x45, 0x00 });

        // Reset size
        if (doubleSize)
            result.AddRange(new byte[] { GS, 0x21, 0x00 });

        // Reset justification to left
        result.AddRange(new byte[] { ESC, 0x61, 0x00 });

        return result.ToArray();
    }

    private static byte[] BuildLine(string text)
    {
        var result = new List<byte>();
        result.AddRange(Encoding.UTF8.GetBytes(text));
        result.Add(LF);
        return result.ToArray();
    }

    private static byte[] BuildRightText(string text, bool bold = false, bool doubleSize = false)
    {
        var result = new List<byte>();

        // Right justification
        result.AddRange(new byte[] { ESC, 0x61, 0x02 });

        if (bold)
            result.AddRange(new byte[] { ESC, 0x45, 0x01 });

        if (doubleSize)
            result.AddRange(new byte[] { GS, 0x21, 0x11 });

        result.AddRange(Encoding.UTF8.GetBytes(text));
        result.Add(LF);

        if (bold)
            result.AddRange(new byte[] { ESC, 0x45, 0x00 });

        if (doubleSize)
            result.AddRange(new byte[] { GS, 0x21, 0x00 });

        result.AddRange(new byte[] { ESC, 0x61, 0x00 });

        return result.ToArray();
    }

    private static byte[] BuildSeparatorLine(char separator = '-')
    {
        return BuildLine(new string(separator, 48));
    }

    private static byte[] BuildItemLine(string name, decimal quantity, decimal unitPrice, decimal totalPrice)
    {
        // Truncate or pad name to 24 chars for alignment
        var displayName = name.Length > 24 ? name[..24] : name.PadRight(24);
        var line = $"{displayName}{quantity,5:N0}{unitPrice,10:N3}{totalPrice,10:N3}";
        return BuildLine(line);
    }

    private static byte[] BuildBarcode(string data)
    {
        var result = new List<byte>();

        // CODE128 barcode type
        result.AddRange(new byte[] { GS, 0x68, 0x02 }); // Barcode type: CODE128

        // Barcode data
        var dataBytes = Encoding.UTF8.GetBytes(data);
        result.AddRange(new byte[] { GS, 0x6B, 0x02 }); // Function B for CODE128
        result.AddRange(dataBytes);
        result.Add(0x00); // Null terminator

        // Print barcode below
        result.AddRange(new byte[] { GS, 0x48, 0x02 }); // Print HRI below barcode

        // Barcode width: 2
        result.AddRange(new byte[] { GS, 0x77, 0x02 });

        // Barcode height: 60 dots
        result.AddRange(new byte[] { GS, 0x68, 0x3C });

        return result.ToArray();
    }

    // ============================================================
    // SendToPrinterAsync — Hardware Communication Dispatch
    // ============================================================

    /// <summary>
    /// Dispatches ESC/POS commands to the printer based on its connection type.
    /// Falls back to raw byte logging when no connection info is configured (test mode).
    /// </summary>
    private async Task SendToPrinterAsync(Printer printer, List<byte[]> commands)
    {
        if (commands == null || commands.Count == 0)
        {
            _logger.LogWarning("No commands to send to printer {PrinterName}", printer.Name);
            return;
        }

        var totalBytes = commands.Sum(c => c.Length);
        _logger.LogDebug(
            "Sending {CommandCount} commands ({TotalBytes} bytes) to printer {PrinterName} via {Connection}",
            commands.Count, totalBytes, printer.Name, printer.Connection);

        switch (printer.Connection)
        {
            case PrinterConnection.Network:
                await _hardwareSender.SendViaNetworkAsync(printer, commands);
                break;

            case PrinterConnection.Serial:
                await _hardwareSender.SendViaSerialAsync(printer, commands);
                break;

            case PrinterConnection.USB:
                await _hardwareSender.SendViaUsbAsync(printer, commands);
                break;

            default:
                // Fallback: log commands for testing/development
                _logger.LogInfo(
                    "Printer {PrinterName} has no known connection type. Commands logged ({TotalBytes} bytes).",
                    printer.Name, totalBytes);
                await Task.Delay(100); // Simulate print delay
                break;
        }
    }

    // ============================================================
    // Cash Drawer
    // ============================================================

    /// <summary>
    /// Opens the cash drawer connected to the printer.
    /// Sends the ESC/POS cash drawer kick command: ESC p m t1 t2
    /// Pin 0 = drawer 2, Pin 1 = drawer 5 (default pin 0 with 100ms pulse).
    /// </summary>
    public async Task<bool> OpenCashDrawerAsync(Printer printer)
    {
        ArgumentNullException.ThrowIfNull(printer);
        try
        {
            // ESC p m t1 t2
            // m = 0 (drawer pin 2), t1 = 50 (100ms), t2 = 50 (100ms)
            byte[] command = [0x1B, 0x70, 0x00, 0x32, 0x32];

            var commands = new List<byte[]>
            {
                new byte[] { ESC, 0x40 }, // Initialize
                command                     // Open cash drawer
            };

            await SendToPrinterAsync(printer, commands);
            _logger.LogInfo("Cash drawer opened via printer {PrinterName}", printer.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to open cash drawer via printer {printer.Name}: {ex.Message}", ex);
            return false;
        }
    }

}