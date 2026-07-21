using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IPrinterManagementService
{
    Task<List<PrinterDto>> GetPrintersAsync();
    Task<PrinterDto> AddPrinterAsync(string name, string printerType, string connection, string? ipAddress, string? port, int paperWidth, string role);
    Task<OperationResult> UpdatePrinterAsync(PrinterDto printer);
    Task<OperationResult> DeletePrinterAsync(Guid id);
    Task<bool> TestPrinterAsync(Guid id);
    Task<List<KitchenStationDto>> GetKitchenStationsAsync();
    Task<KitchenStationDto> AddKitchenStationAsync(string name, Guid? printerId);

    /// <summary>
    /// Prints a receipt for a completed sale.
    /// Finds the first active receipt printer, fetches sale and settings data, and calls the hardware printer.
    /// </summary>
    Task<bool> PrintReceiptAsync(Guid saleId);

    /// <summary>
    /// Prints kitchen/beverage tickets for a completed sale.
    /// Groups sale items by KitchenStationId, finds the printer assigned to each station,
    /// and sends a filtered ticket containing only that station's items.
    /// Returns true if ALL kitchen tickets printed successfully.
    /// </summary>
    Task<bool> PrintKitchenTicketsAsync(Guid saleId);

    /// <summary>
    /// Opens the cash drawer connected to the first active receipt printer.
    /// Sends the ESC/POS cash drawer kick command (ESC p m t1 t2).
    /// </summary>
    Task<bool> OpenCashDrawerAsync();
}