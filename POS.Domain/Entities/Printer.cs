using POS.Domain.Enums;

namespace POS.Domain.Entities;

public class Printer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ArabicName { get; set; }
    public PrinterType PrinterType { get; set; }
    public PrinterConnection Connection { get; set; }
    public string? IpAddress { get; set; }
    public int Port { get; set; }
    public string? ConnectionString { get; set; }
    public int PaperWidth { get; set; } = 80;

    /// <summary>Serial port baud rate (e.g., 9600, 19200, 38400, 115200). Default: 9600 (standard for ESC/POS thermal printers).</summary>
    public int BaudRate { get; set; } = 9600;

    public string Encoding { get; set; } = "UTF-8";
    public PrinterRole AssignedRole { get; set; }
    public Guid? StationId { get; set; }
    public bool IsActive { get; set; } = true;
}
