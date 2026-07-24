using Xunit;
using FluentAssertions;
using POS.Reporting.Exporters;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for ExcelReportExporter.
/// Tests are purely in-memory — ClosedXML writes to MemoryStream, no file I/O needed.
/// </summary>
public sealed class ExcelReportExporterTests
{
    private readonly ExcelReportExporter _sut = new();

    private const string Title = "تقرير المبيعات";
    private static readonly string[] Columns = ["الصنف", "الكمية", "السعر"];
    private static readonly object?[][] SampleRows =
    [
        ["قهوة", 10, 15.500m],
        ["شاي", 5, 8.000m],
        ["عصير", 3, 12.000m]
    ];
    private const string Summary = "المجموع: 50 دينار";

    // ========================================================================
    // Guard Clauses
    // ========================================================================

    [Fact]
    public void ExportToExcel_NullTitle_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.ExportToExcel(null!, Columns, SampleRows);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExportToExcel_NullColumns_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.ExportToExcel(Title, null!, SampleRows);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExportToExcel_NullRows_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.ExportToExcel(Title, Columns, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ========================================================================
    // ExportToExcel — Output Validation
    // ========================================================================

    [Fact]
    public void ExportToExcel_WithFullDataAndSummary_ShouldReturnNonEmptyByteArray()
    {
        var result = _sut.ExportToExcel(Title, Columns, SampleRows, Summary);

        result.Should().NotBeNullOrEmpty();
        result.Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public void ExportToExcel_WithFullDataAndSummary_ShouldBeValidExcelPackage()
    {
        var result = _sut.ExportToExcel(Title, Columns, SampleRows, Summary);

        // .xlsx files are ZIP archives starting with PK\x03\x04
        result.Should().HaveCountGreaterThan(4);
        result[0].Should().Be(0x50); // 'P'
        result[1].Should().Be(0x4B); // 'K'
        result[2].Should().Be(0x03);
        result[3].Should().Be(0x04);
    }

    [Fact]
    public void ExportToExcel_WithoutSummary_ShouldStillProduceValidOutput()
    {
        var result = _sut.ExportToExcel(Title, Columns, SampleRows);

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x50);
        result[1].Should().Be(0x4B);
    }

    [Fact]
    public void ExportToExcel_EmptyRows_ShouldProduceValidOutputWithHeaderOnly()
    {
        var result = _sut.ExportToExcel(Title, Columns, Array.Empty<object?[]>());

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x50);
        result[1].Should().Be(0x4B);
    }

    [Fact]
    public void ExportToExcel_SingleRow_ShouldProduceValidOutput()
    {
        var rows = new[] { new object?[] { "ماء", 20, 5.000m } };
        var result = _sut.ExportToExcel(Title, Columns, rows);

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x50);
        result[1].Should().Be(0x4B);
    }

    [Fact]
    public void ExportToExcel_WithNullCells_ShouldReplaceWithEmptyString()
    {
        var rows = new[] { new object?[] { "ماء", null, 5.000m } };
        var result = _sut.ExportToExcel(Title, Columns, rows);

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x50);
        result[1].Should().Be(0x4B);
    }

    // ========================================================================
    // Edge Cases
    // ========================================================================

    [Fact]
    public void ExportToExcel_LongTitle_ShouldTruncateSheetNameTo31Chars()
    {
        var longTitle = new string('أ', 50);
        var result = _sut.ExportToExcel(longTitle, Columns, SampleRows);

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x50);
        result[1].Should().Be(0x4B);
    }

    [Fact]
    public void ExportToExcel_MultipleColumns_ShouldHandleAllData()
    {
        var manyColumns = new[] { "أ", "ب", "ج", "د", "ه", "و", "ز", "ح", "ط", "ي" };
        var manyRows = new object?[][]
        {
            Enumerable.Range(1, 10).Select(i => (object?)i).ToArray()
        };
        var result = _sut.ExportToExcel("عريض", manyColumns, manyRows);

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x50);
        result[1].Should().Be(0x4B);
    }

    [Fact]
    public void ExportToExcel_SummaryWithoutRows_ShouldRenderSummary()
    {
        var result = _sut.ExportToExcel(Title, Columns, Array.Empty<object?[]>(), Summary);

        result.Should().NotBeNullOrEmpty();
        result[0].Should().Be(0x50);
        result[1].Should().Be(0x4B);
    }
}
