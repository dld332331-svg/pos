using POS.Application.DTOs;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class PrinterManagementService : IPrinterManagementService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPrinterService _printerService;
    private readonly IAuditService _auditService;

    public PrinterManagementService(IUnitOfWork unitOfWork, IPrinterService printerService, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _printerService = printerService;
        _auditService = auditService;
    }

    public async Task<List<PrinterDto>> GetPrintersAsync()
    {
        var printers = await _unitOfWork.Printers.GetAllAsync();
        return printers.Select(p => new PrinterDto(
            p.Id,
            p.Name,
            p.PrinterType.ToString(),
            p.Connection.ToString(),
            p.IpAddress,
            p.Port > 0 ? p.Port.ToString() : null,
            p.PaperWidth,
            p.AssignedRole.ToString(),
            p.IsActive)).ToList();
    }

    public async Task<PrinterDto> AddPrinterAsync(
        string name, string printerType, string connection, string? ipAddress, string? port,
        int paperWidth, string role)
    {
        if (!Enum.TryParse<PrinterType>(printerType, ignoreCase: true, out var pType))
            throw new InvalidOperationException("نوع الطابعة غير صالح");

        if (!Enum.TryParse<PrinterConnection>(connection, ignoreCase: true, out var pConn))
            throw new InvalidOperationException("نوع الاتصال غير صالح");

        if (!Enum.TryParse<PrinterRole>(role, ignoreCase: true, out var pRole))
            throw new InvalidOperationException("دور الطابعة غير صالح");

        int portNum = 0;
        if (!string.IsNullOrWhiteSpace(port) && !int.TryParse(port, out portNum))
            portNum = 9100; // Default network printer port

        var printer = new Printer
        {
            Name = name,
            PrinterType = pType,
            Connection = pConn,
            IpAddress = ipAddress,
            Port = portNum,
            PaperWidth = paperWidth > 0 ? paperWidth : 80,
            AssignedRole = pRole,
            IsActive = true
        };

        await _unitOfWork.Printers.AddAsync(printer);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "Printer", printer.Id,
            null, $"Name={name},Type={pType},Connection={pConn},Role={pRole}", null);

        return new PrinterDto(
            printer.Id,
            printer.Name,
            printer.PrinterType.ToString(),
            printer.Connection.ToString(),
            printer.IpAddress,
            printer.Port > 0 ? printer.Port.ToString() : null,
            printer.PaperWidth,
            printer.AssignedRole.ToString(),
            printer.IsActive);
    }

    public async Task<OperationResult> UpdatePrinterAsync(PrinterDto printer)
    {
        ArgumentNullException.ThrowIfNull(printer);
        var existing = await _unitOfWork.Printers.GetByIdAsync(printer.Id);
        if (existing is null)
            return new OperationResult(false, ErrorMessage: "الطابعة غير موجودة");

        var beforeValue = $"Name={existing.Name},Active={existing.IsActive}";

        existing.Name = printer.Name;

        if (Enum.TryParse<PrinterType>(printer.PrinterType, ignoreCase: true, out var pType))
            existing.PrinterType = pType;

        if (Enum.TryParse<PrinterConnection>(printer.Connection, ignoreCase: true, out var pConn))
            existing.Connection = pConn;

        existing.IpAddress = printer.IpAddress;
        existing.PaperWidth = printer.PaperWidth;
        existing.IsActive = printer.IsActive;

        if (Enum.TryParse<PrinterRole>(printer.AssignedRole, ignoreCase: true, out var pRole))
            existing.AssignedRole = pRole;

        if (int.TryParse(printer.Port, out var portNum))
            existing.Port = portNum;

        existing.MarkAsModified();

        await _unitOfWork.Printers.UpdateAsync(existing);
        await _unitOfWork.SaveChangesAsync();

        var afterValue = $"Name={existing.Name},Active={existing.IsActive}";
        await _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "Printer", existing.Id,
            beforeValue, afterValue, null);

        return new OperationResult(true, SuccessMessage: "تم تحديث الطابعة بنجاح");
    }

    public async Task<OperationResult> DeletePrinterAsync(Guid id)
    {
        var printer = await _unitOfWork.Printers.GetByIdAsync(id);
        if (printer is null)
            return new OperationResult(false, ErrorMessage: "الطابعة غير موجودة");

        var beforeValue = $"Name={printer.Name}";
        printer.MarkAsDeleted();

        await _unitOfWork.Printers.UpdateAsync(printer);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "Printer", id,
            beforeValue, null, "Printer deleted");

        return new OperationResult(true, SuccessMessage: "تم حذف الطابعة بنجاح");
    }

    public async Task<bool> TestPrinterAsync(Guid id)
    {
        var printer = await _unitOfWork.Printers.GetByIdAsync(id);
        if (printer is null) return false;

        return await _printerService.TestPrinterAsync(printer);
    }

    public async Task<bool> PrintReceiptAsync(Guid saleId)
    {
        try
        {
            // Find the first active receipt printer
            var printers = await _unitOfWork.Printers.FindAsync(p =>
                p.AssignedRole == PrinterRole.Receipt && p.IsActive);
            var receiptPrinter = printers.FirstOrDefault();
            if (receiptPrinter == null)
            {
                _ = _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "Printer", null,
                    null, null, $"No active receipt printer found for sale {saleId}");
                return false;
            }

            // Fetch the completed sale with related data
            var sale = await _unitOfWork.Sales.GetByIdAsync(saleId);
            if (sale == null)
            {
                _ = _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "Sale", saleId,
                    null, null, "Sale not found for receipt printing");
                return false;
            }

            // Load related entities for receipt content
            var user = await _unitOfWork.Users.FindAsync(u => u.Id == sale.UserId);
            if (user.FirstOrDefault() is User saleUser)
                sale.User = saleUser;

            if (sale.TableId.HasValue)
            {
                var tables = await _unitOfWork.Tables.FindAsync(t => t.Id == sale.TableId.Value);
                if (tables.FirstOrDefault() is Table saleTable)
                    sale.Table = saleTable;
            }

            if (sale.CustomerId.HasValue)
            {
                var customers = await _unitOfWork.Customers.FindAsync(c => c.Id == sale.CustomerId.Value);
                if (customers.FirstOrDefault() is Customer saleCustomer)
                    sale.Customer = saleCustomer;
            }

            // Load sale items
            var saleItems = await _unitOfWork.SaleItems.FindAsync(si => si.SaleId == saleId);
            foreach (var si in saleItems)
            {
                sale.AddItem(si);

                // Load modifiers for each item
                var modifiers = await _unitOfWork.SaleItemModifiers.FindAsync(m => m.SaleItemId == si.Id);
                foreach (var mod in modifiers)
                    si.AddModifier(mod);
            }

            // Load payments
            var payments = await _unitOfWork.Payments.FindAsync(p => p.SaleId == saleId);
            foreach (var p in payments)
                sale.AddPayment(p);

            // Fetch store settings for the receipt header/footer
            var settings = await _unitOfWork.Settings.FindAsync(s =>
                s.Key == "StoreName" ||
                s.Key == "StoreNameArabic" ||
                s.Key == "ReceiptFooter" ||
                s.Key == "ReceiptFooterArabic");
            var settingsDict = settings.ToDictionary(s => s.Key, s => s.Value);

            var storeName = settingsDict.GetValueOrDefault("StoreName", "My Store");
            var storeNameArabic = settingsDict.GetValueOrDefault("StoreNameArabic", "متجري");
            var footerMsg = settingsDict.GetValueOrDefault("ReceiptFooter", "Thank you for your purchase!");
            var footerMsgArabic = settingsDict.GetValueOrDefault("ReceiptFooterArabic", "شكراً لشرائك!");

            // Call the hardware printer service
            var result = await _printerService.PrintReceiptAsync(
                receiptPrinter, sale, storeName, storeNameArabic, footerMsg, footerMsgArabic);

            if (result)
            {
                _ = _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "Sale", saleId,
                    null, null, $"Receipt printed for sale {sale.InvoiceNumber} on printer {receiptPrinter.Name}");
            }
            else
            {
                _ = _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "Printer", receiptPrinter.Id,
                    null, null, $"Receipt printing failed for sale {saleId}: printer error");
            }

            return result;
        }
        catch (Exception ex)
        {
            _ = _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "Printer", null,
                null, null, $"Receipt printing error for sale {saleId}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> PrintKitchenTicketsAsync(Guid saleId)
    {
        try
        {
            // Fetch the completed sale
            var sale = await _unitOfWork.Sales.GetByIdAsync(saleId);
            if (sale == null)
            {
                _ = _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "Sale", saleId,
                    null, null, "Sale not found for kitchen ticket printing");
                return false;
            }

            // Load the cashier
            var users = await _unitOfWork.Users.FindAsync(u => u.Id == sale.UserId);
            if (users.FirstOrDefault() is User saleUser)
                sale.User = saleUser;

            // Load table if assigned
            if (sale.TableId.HasValue)
            {
                var tables = await _unitOfWork.Tables.FindAsync(t => t.Id == sale.TableId.Value);
                if (tables.FirstOrDefault() is Table saleTable)
                    sale.Table = saleTable;
            }

            // Load sale items with modifiers
            var saleItems = await _unitOfWork.SaleItems.FindAsync(si => si.SaleId == saleId);
            foreach (var si in saleItems)
            {
                sale.AddItem(si);
                var modifiers = await _unitOfWork.SaleItemModifiers.FindAsync(m => m.SaleItemId == si.Id);
                foreach (var mod in modifiers)
                    si.AddModifier(mod);
            }

            // Group items by KitchenStationId
            var stationGroups = saleItems
                .Where(si => si.KitchenStationId.HasValue)
                .GroupBy(si => si.KitchenStationId!.Value)
                .ToList();

            if (stationGroups.Count == 0)
            {
                // No items need kitchen printing — that's fine
                return true;
            }

            // Fetch all kitchen stations and printers
            var allStations = await _unitOfWork.KitchenStations.GetAllAsync();
            var stationMap = allStations.Where(s => s.IsActive).ToDictionary(s => s.Id, s => s);

            var kitchenPrinters = await _unitOfWork.Printers.FindAsync(p =>
                (p.AssignedRole == PrinterRole.Kitchen || p.AssignedRole == PrinterRole.Beverage) && p.IsActive);
            var printerMap = kitchenPrinters.ToDictionary(p => p.Id, p => p);

            bool allSucceeded = true;

            foreach (var group in stationGroups)
            {
                var stationId = group.Key;

                if (!stationMap.TryGetValue(stationId, out var station))
                {
                    _ = _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "KitchenStation", stationId,
                        null, null, $"Kitchen station {stationId} not found or inactive for sale {saleId}");
                    allSucceeded = false;
                    continue;
                }

                if (!station.PrinterId.HasValue || !printerMap.TryGetValue(station.PrinterId.Value, out var printer))
                {
                    _ = _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "Printer", station.PrinterId,
                        null, null, $"No active printer for kitchen station '{station.Name}' (sale {saleId})");
                    allSucceeded = false;
                    continue;
                }

                // Create a filtered sale copy with only this station's items
                var filteredSale = new Sale
                {
                    Id = sale.Id,
                    InvoiceNumber = sale.InvoiceNumber,
                    CreatedAt = sale.CreatedAt,
                    User = sale.User,
                    Table = sale.Table,
                    Notes = sale.Notes
                };
                foreach (var si in group)
                    filteredSale.AddItem(si);

                var success = await _printerService.PrintKitchenTicketAsync(printer, filteredSale, station.Name);

                if (success)
                {
                    _ = _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "Sale", saleId,
                        null, null, $"Kitchen ticket printed for sale {sale.InvoiceNumber} at station '{station.Name}' on printer '{printer.Name}'");
                }
                else
                {
                    _ = _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "Printer", printer.Id,
                        null, null, $"Kitchen ticket printing failed for sale {saleId} at station '{station.Name}': printer error");
                    allSucceeded = false;
                }
            }

            return allSucceeded;
        }
        catch (Exception ex)
        {
            _ = _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "Printer", null,
                null, null, $"Kitchen ticket printing error for sale {saleId}: {ex.Message}");
            return false;
        }
    }

    public async Task<List<KitchenStationDto>> GetKitchenStationsAsync()
    {
        var stations = await _unitOfWork.KitchenStations.GetAllAsync();
        var printers = await _unitOfWork.Printers.GetAllAsync();
        var printerMap = printers.ToDictionary(p => p.Id, p => (string?)p.Name);

        return stations.Select(s => new KitchenStationDto(
            s.Id,
            s.Name,
            s.IsActive,
            s.PrinterId,
            s.PrinterId.HasValue ? printerMap.GetValueOrDefault(s.PrinterId.Value, null) : null)).ToList();
    }

    public async Task<bool> OpenCashDrawerAsync()
    {
        try
        {
            // Find the first active receipt printer
            var printers = await _unitOfWork.Printers.FindAsync(p =>
                p.AssignedRole == PrinterRole.Receipt && p.IsActive);
            var receiptPrinter = printers.FirstOrDefault();
            if (receiptPrinter == null)
            {
                await _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "Printer", null,
                    null, null, "No active receipt printer found for cash drawer");
                return false;
            }

            var result = await _printerService.OpenCashDrawerAsync(receiptPrinter);

            if (result)
            {
                await _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "Printer", receiptPrinter.Id,
                    null, null, $"Cash drawer opened via printer {receiptPrinter.Name}");
            }
            else
            {
                await _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "Printer", receiptPrinter.Id,
                    null, null, $"Cash drawer open failed on printer {receiptPrinter.Name}");
            }

            return result;
        }
        catch (Exception ex)
        {
            await _auditService.LogAsync(null, AuditActionType.PrinterConfigChanged, "Printer", null,
                null, null, $"Cash drawer open error: {ex.Message}");
            return false;
        }
    }

    public async Task<KitchenStationDto> AddKitchenStationAsync(string name, Guid? printerId)
    {
        var station = new KitchenStation
        {
            Name = Domain.ValueObjects.ArabicName.Create(name),
            PrinterId = printerId,
            IsActive = true
        };

        await _unitOfWork.KitchenStations.AddAsync(station);
        await _unitOfWork.SaveChangesAsync();

        string? printerName = null;
        if (printerId.HasValue)
        {
            var printer = await _unitOfWork.Printers.GetByIdAsync(printerId.Value);
            printerName = printer?.Name;
        }

        return new KitchenStationDto(station.Id, station.Name, station.IsActive, station.PrinterId, printerName);
    }
}