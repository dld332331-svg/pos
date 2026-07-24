using Xunit;
using FluentAssertions;
using POS.Reporting.Exporters;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for ReportExporter — the wrapper that delegates to
/// PdfReportExporter (ExportToPdf) and ExcelReportExporter (ExportToExcel).
/// </summary>
public sealed class ReportExporterTests
{
    private readonly ReportExporter _sut = new();

    private const string Title = "تقرير المبيعات";
    private static readonly string[] Columns = ["الصنف", "الكمية", "السعر"];
    private static readonly object?[][] SampleRows =
    [
        ["قهوة", 10, 15.500m],
        ["شاي", 5, 8.000m]
    ];
    private const string Summary = "المجموع: 50 دينار";

    // ========================================================================
    // ExportToPdf — Delegation
    // ========================================================================

    [Fact]
    public void ExportToPdf_WithFullData_ShouldReturnValidPdfBytes()
    {
        var result = _sut.ExportToPdf(Title, Columns, SampleRows, Summary);

        result.Should().NotBeNullOrEmpty();
        // PDF starts with %PDF
        result[0].Should().Be(0x25); // '%'
        result[1].Should().Be(0x50); // 'P'
        result[2].Should().Be(0x44); // 'D'
        result[3].Should().Be(0x46); // 'F'
    }

    [Fact]
    public void ExportToPdf_WithoutSummary_ShouldStillReturnValidPdf()
    {
        var result = _sut.ExportToPdf(Title, Columns, SampleRows);

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x25);
        result[1].Should().Be(0x50);
    }

    [Fact]
    public void ExportToPdf_EmptyRows_ShouldReturnValidPdfWithHeader()
    {
        var result = _sut.ExportToPdf(Title, Columns, Array.Empty<object?[]>());

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x25);
        result[1].Should().Be(0x50);
    }

    // ========================================================================
    // ExportToExcel — Delegation
    // ========================================================================

    [Fact]
    public void ExportToExcel_WithFullData_ShouldReturnValidExcelBytes()
    {
        var result = _sut.ExportToExcel(Title, Columns, SampleRows, Summary);

        result.Should().NotBeNullOrEmpty();
        // .xlsx files are ZIP archives starting with PK\x03\x04
        result[0].Should().Be(0x50); // 'P'
        result[1].Should().Be(0x4B); // 'K'
        result[2].Should().Be(0x03);
        result[3].Should().Be(0x04);
    }

    [Fact]
    public void ExportToExcel_WithoutSummary_ShouldStillReturnValidExcel()
    {
        var result = _sut.ExportToExcel(Title, Columns, SampleRows);

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x50);
        result[1].Should().Be(0x4B);
    }

    [Fact]
    public void ExportToExcel_EmptyRows_ShouldReturnValidExcelWithHeader()
    {
        var result = _sut.ExportToExcel(Title, Columns, Array.Empty<object?[]>());

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x50);
        result[1].Should().Be(0x4B);
    }

    [Fact]
    public void ExportToExcel_SingleRow_ShouldReturnValidExcel()
    {
        var rows = new[] { new object?[] { "ماء", 20, 5.000m } };
        var result = _sut.ExportToExcel(Title, Columns, rows);

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x50);
        result[1].Should().Be(0x4B);
    }
}
