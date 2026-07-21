using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace POS.Reporting.Exporters;

public class PdfReportExporter
{
    public PdfReportExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] ExportToPdf(string title, string[] columns, object?[][] rows, string? summary = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(c => ComposeHeader(c, title));
                page.Content().Element(c => ComposeContent(c, columns, rows));
                if (summary is not null)
                    page.Footer().Element(c => ComposeFooter(c, summary));
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, string title)
    {
        container.Column(col =>
        {
            col.Item().Text(title).SemiBold().FontSize(16).AlignRight();
            col.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
        });
    }

    private static void ComposeContent(IContainer container, string[] columns, object?[][] rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                foreach (var _ in columns)
                {
                    cols.RelativeColumn();
                }
            });

            table.Header(header =>
            {
                foreach (var col in columns)
                {
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4)
                        .Text(col).SemiBold().FontSize(9).AlignRight();
                }
            });

            foreach (var row in rows)
            {
                foreach (var cell in row)
                {
                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3)
                        .Text(cell?.ToString() ?? "").FontSize(8).AlignRight();
                }
            }
        });
    }

    private static void ComposeFooter(IContainer container, string summary)
    {
        container.AlignRight().Text(summary).FontSize(10).SemiBold();
    }
}
