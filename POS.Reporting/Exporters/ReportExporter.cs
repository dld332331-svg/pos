using POS.Application.Services;

namespace POS.Reporting.Exporters;

public class ReportExporter : IReportExporter
{
    private readonly PdfReportExporter _pdf = new();
    private readonly ExcelReportExporter _excel = new();

    public byte[] ExportToPdf(string title, string[] columns, object?[][] rows, string? summary = null)
        => _pdf.ExportToPdf(title, columns, rows, summary);

    public byte[] ExportToExcel(string title, string[] columns, object?[][] rows, string? summary = null)
        => _excel.ExportToExcel(title, columns, rows, summary);
}
