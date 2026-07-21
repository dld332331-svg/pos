using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Domain.Interfaces;

/// <summary>
/// Service for interacting with hardware printers in the POS system.
/// </summary>
public interface IPrinterService
{
    Task<bool> PrintReceiptAsync(Printer printer, Sale sale, string storeName, string storeNameArabic, string footerMessage = "", string footerMessageArabic = "");
    Task<bool> PrintKitchenTicketAsync(Printer printer, Sale sale, string kitchenStationName);
    Task<bool> TestPrinterAsync(Printer printer);

    /// <summary>
    /// Opens the cash drawer connected to the specified printer.
    /// Sends the ESC/POS cash drawer kick command (ESC p m t1 t2).
    /// </summary>
    Task<bool> OpenCashDrawerAsync(Printer printer);

    PrinterStatus GetPrinterStatus(Printer printer);
}