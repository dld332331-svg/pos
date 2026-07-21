namespace POS.Application.Services;

public interface IReportExporter
{
    byte[] ExportToPdf(string title, string[] columns, object?[][] rows, string? summary = null);
    byte[] ExportToExcel(string title, string[] columns, object?[][] rows, string? summary = null);
}
