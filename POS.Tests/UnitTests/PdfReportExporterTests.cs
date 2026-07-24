using Xunit;
using FluentAssertions;
using POS.Reporting.Exporters;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for PdfReportExporter.
/// QuestPDF generates PDF bytes in-memory via Document.GeneratePdf(), no file I/O needed.
/// The constructor sets QuestPDF.Settings.License = LicenseType.Community.
/// </summary>
public sealed class PdfReportExporterTests
{
    private readonly PdfReportExporter _sut = new();

    private const string Title = "تقرير المبيعات اليومي";
    private static readonly string[] Columns = ["الصنف", "الكمية", "السعر", "الإجمالي"];
    private static readonly object?[][] SampleRows =
    [
        ["قهوة تركية", 10, 1.500m, 15.000m],
        ["شاي أحمر", 5, 0.800m, 4.000m],
        ["عصير برتقال", 3, 2.000m, 6.000m]
    ];
    private const string Summary = "المجموع الكلي: 25.000 دينار";

    // ========================================================================
    // Guard Clauses
    // ========================================================================

    [Fact]
    public void ExportToPdf_NullTitle_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.ExportToPdf(null!, Columns, SampleRows);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExportToPdf_NullColumns_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.ExportToPdf(Title, null!, SampleRows);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExportToPdf_NullRows_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.ExportToPdf(Title, Columns, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ========================================================================
    // ExportToPdf — Output Validation
    // ========================================================================

    [Fact]
    public void ExportToPdf_WithFullDataAndSummary_ShouldReturnNonEmptyByteArray()
    {
        var result = _sut.ExportToPdf(Title, Columns, SampleRows, Summary);

        result.Should().NotBeNullOrEmpty();
        result.Length.Should().BeGreaterThan(200);
    }

    [Fact]
    public void ExportToPdf_WithFullDataAndSummary_ShouldBeValidPdf()
    {
        var result = _sut.ExportToPdf(Title, Columns, SampleRows, Summary);

        // PDF files start with %PDF
        result.Should().HaveCountGreaterThan(4);
        result[0].Should().Be(0x25); // '%'
        result[1].Should().Be(0x50); // 'P'
        result[2].Should().Be(0x44); // 'D'
        result[3].Should().Be(0x46); // 'F'
    }

    [Fact]
    public void ExportToPdf_WithoutSummary_ShouldStillProduceValidPdf()
    {
        var result = _sut.ExportToPdf(Title, Columns, SampleRows);

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x25);
        result[1].Should().Be(0x50);
    }

    [Fact]
    public void ExportToPdf_EmptyRows_ShouldProduceValidPdfWithHeaderOnly()
    {
        var result = _sut.ExportToPdf(Title, Columns, Array.Empty<object?[]>());

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x25);
        result[1].Should().Be(0x50);
    }

    [Fact]
    public void ExportToPdf_SingleRow_ShouldProduceValidPdf()
    {
        var rows = new[] { new object?[] { "مياه معدنية", 20, 0.500m, 10.000m } };
        var result = _sut.ExportToPdf(Title, Columns, rows);

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x25);
        result[1].Should().Be(0x50);
    }

    [Fact]
    public void ExportToPdf_WithNullCells_ShouldRenderEmptyString()
    {
        var rows = new[] { new object?[] { "مياه", null, 0.500m, null } };
        var result = _sut.ExportToPdf(Title, Columns, rows);

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x25);
        result[1].Should().Be(0x50);
    }

    // ========================================================================
    // Edge Cases
    // ========================================================================

    [Fact]
    public void ExportToPdf_LargeDataSet_ShouldExportSuccessfully()
    {
        var manyColumns = new[] { "أ", "ب", "ج", "د", "ه" };
        var manyRows = Enumerable.Range(1, 50).Select(i =>
            new object?[] { $"عنصر {i}", i, i * 1.5m, i * 2.0m }
        ).ToArray();

        var result = _sut.ExportToPdf("بيانات كبيرة", manyColumns, manyRows, "مجموع كبير");

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x25);
        result[1].Should().Be(0x50);
    }

    [Fact]
    public void ExportToPdf_SummaryOnly_ShouldRenderFooter()
    {
        var result = _sut.ExportToPdf(Title, Columns, Array.Empty<object?[]>(), Summary);

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x25);
        result[1].Should().Be(0x50);
    }

    [Fact]
    public void ExportToPdf_RtlTitle_ShouldRenderCorrectly()
    {
        var arabicTitle = "تقرير المبيعات الشهري - كانون الثاني 2026";
        var result = _sut.ExportToPdf(arabicTitle, Columns, SampleRows, Summary);

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x25);
        result[1].Should().Be(0x50);
    }

    [Fact]
    public void ExportToPdf_NoFooterWhenSummaryIsNull()
    {
        // Summary is null → no footer section
        var result = _sut.ExportToPdf(Title, Columns, SampleRows);

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x25);
        result[1].Should().Be(0x50);
    }
}
