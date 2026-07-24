using BenchmarkDotNet.Attributes;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Printing;

namespace POS.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for ESCPOSPrinter — the critical printing path.
///
/// Measures command byte construction for:
///   1. PrintReceiptAsync — full ESC/POS receipt with headers, items, totals, barcode
///   2. PrintKitchenTicketAsync — kitchen order ticket
///   3. TestPrinterAsync — printer test page
///
/// All three benchmarks build the full command byte sequences BEFORE
/// any hardware dispatch attempt. The dispatch path uses the fallback
/// (non-matching PrinterConnection → default: case) which logs commands
/// and does a simulated 100ms print delay, avoiding actual I/O.
/// Memory allocation diagnostics are unaffected by the simulated delay.
/// </summary>
[MemoryDiagnoser]
public class ESCPOSPrinterBenchmarks
{
    [Params(1, 5, 25)]
    public int ItemCount { get; set; }

    private ESCPOSPrinter _printer = null!;
    private TestLogger _logger = null!;
    private Printer _printerEntity = null!;
    private Sale _sale = null!;
    private static readonly Guid UserId = Guid.NewGuid();
    [GlobalSetup]
    public void Setup()
    {
        _logger = new TestLogger();
        var hardwareSender = new RealPrinterHardwareSender(_logger, 1);
        _printer = new ESCPOSPrinter(_logger, hardwareSender);

        // Use non-matching PrinterConnection value (99) so SendToPrinterAsync
        // falls through to the default: no-op fallback path.
        _printerEntity = new Printer
        {
            Id = Guid.NewGuid(),
            Name = "Bench Printer",
            Connection = (PrinterConnection)99,
            Port = 0,
            IsActive = true,
            PaperWidth = 80
        };

        var user = new User
        {
            Id = UserId,
            FullName = "Bench User",
            Username = "bench",
            Role = UserRole.Cashier,
            IsActive = true
        };

        // Build sale using AddItem/AddPayment for proper entity initialization
        _sale = new Sale
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "BENCH-20260720-0001",
            CreatedAt = DateTime.UtcNow,
            UserId = UserId,
            User = user,
            Status = SaleStatus.Completed,
            IsPaid = true,
            SubTotal = 15.000m * ItemCount,
            TaxAmount = 15.000m * ItemCount * 0.16m,
            DiscountAmount = ItemCount > 10 ? 5.000m : 0,
            TotalAmount = 15.000m * ItemCount * 1.16m
        };

        for (int i = 0; i < ItemCount; i++)
        {
            var item = new SaleItem
            {
                Id = Guid.NewGuid(),
                SaleId = _sale.Id,
                ProductId = Guid.NewGuid(),
                ProductName = $"Item {i + 1}",
                ProductArabicName = $"عنصر {i + 1}",
                Quantity = 1 + (i % 3),
                UnitPrice = 15.000m,
                TotalPrice = 15.000m * (1 + (i % 3)),
                TaxRate = 0.16m,
                TaxAmount = 15.000m * (1 + (i % 3)) * 0.16m,
                Discount = 0,
                LineTotal = 15.000m * (1 + (i % 3)) * 1.16m,
                Cost = 8.000m,
                Notes = i == 0 ? "No onion" : null
            };

            // Every 3rd item gets a modifier
            if (i % 3 == 0)
            {
                item.AddModifier(new SaleItemModifier
                {
                    Id = Guid.NewGuid(),
                    SaleItemId = item.Id,
                    ModifierName = "Extra Cheese",
                    ModifierArabicName = "جبنة إضافية",
                    AdditionalPrice = 2.000m,
                    Quantity = 1,
                    SizeName = "Large"
                });
            }

            _sale.AddItem(item);
        }

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            SaleId = _sale.Id,
            PaymentMethod = PaymentMethod.Cash,
            Amount = _sale.TotalAmount,
            Timestamp = DateTime.UtcNow
        };
        _sale.AddPayment(payment);
    }

    [Benchmark]
    [BenchmarkCategory("ESCPOS", "PrintReceipt")]
    public async Task<bool> PrintReceipt()
    {
        return await _printer.PrintReceiptAsync(
            _printerEntity, _sale, "Bench Store", "متجر اختباري",
            "Thank you!", "شكراً");
    }

    [Benchmark]
    [BenchmarkCategory("ESCPOS", "PrintKitchenTicket")]
    public async Task<bool> PrintKitchenTicket()
    {
        return await _printer.PrintKitchenTicketAsync(
            _printerEntity, _sale, "Main Kitchen");
    }

    [Benchmark]
    [BenchmarkCategory("ESCPOS", "TestPrint")]
    public async Task<bool> TestPrint()
    {
        return await _printer.TestPrinterAsync(_printerEntity);
    }
}
