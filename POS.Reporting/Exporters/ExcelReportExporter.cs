using ClosedXML.Excel;

namespace POS.Reporting.Exporters;

public class ExcelReportExporter
{
    public byte[] ExportToExcel(string title, string[] columns, object?[][] rows, string? summary = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(title.Length > 31 ? title[..31] : title);

        ws.Cell(1, 1).Value = title;
        ws.Range(1, 1, 1, columns.Length).Merge().Style.Font.Bold = true;

        for (int c = 0; c < columns.Length; c++)
        {
            ws.Cell(2, c + 1).Value = columns[c];
            ws.Cell(2, c + 1).Style.Font.Bold = true;
            ws.Cell(2, c + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        for (int r = 0; r < rows.Length; r++)
        {
            for (int c = 0; c < rows[r].Length && c < columns.Length; c++)
            {
                ws.Cell(r + 3, c + 1).Value = rows[r][c]?.ToString() ?? "";
            }
        }

        ws.Columns().AdjustToContents();

        if (summary is not null)
        {
            var summaryRow = rows.Length + 4;
            ws.Cell(summaryRow, 1).Value = summary;
            ws.Range(summaryRow, 1, summaryRow, columns.Length).Merge().Style.Font.Bold = true;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
