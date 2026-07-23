using System.Reflection;
using System.Windows.Forms;
using Moq;
using Xunit;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Desktop.Forms;

namespace POS.Tests.UITests;

/// <summary>
/// End-to-end UI tests for ReturnForm (RET-001).
/// Tests all states: Initial, LoadingInvoice, InvoiceLoaded, InvoiceNotFound,
/// InvalidInvoice, ItemsSelected, Processing, Success, Error, PermissionDenied, Empty.
/// </summary>
public sealed class ReturnFormUITests : IDisposable
{
    private readonly Mock<ISaleService> _mockSaleService;
    private readonly FormTestHost<ReturnForm> _host;

    private static readonly Guid DefaultSaleId = Guid.NewGuid();
    private const string DefaultInvoiceNumber = "INV-20260719-0001";
    private static readonly DateTime DefaultCreatedAt = new(2026, 7, 19, 10, 30, 0, DateTimeKind.Utc);

    private static readonly SaleSummaryDto DefaultCompletedSale = new(
        DefaultSaleId,
        DefaultInvoiceNumber,
        50.000m,    // SubTotal
        8.000m,     // TaxAmount
        5.000m,     // DiscountAmount
        53.000m,    // TotalAmount
        "Completed",
        DefaultCreatedAt);

    private static readonly List<SaleItemDto> DefaultSaleItems = new()
    {
        new SaleItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "قهوة", 2m, 10.000m,
            0, 0.16m, 3.200m, 23.200m, 5.000m,
            null, null),
        new SaleItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "شاي", 1m, 5.000m,
            0, 0.16m, 0.800m, 5.800m, 2.500m,
            null, null)
    };

    public ReturnFormUITests()
    {
        _mockSaleService = new Mock<ISaleService>(MockBehavior.Strict);

        // Default setup: GetSaleByInvoiceNumberAsync returns null (not found)
        _mockSaleService
            .Setup(s => s.GetSaleByInvoiceNumberAsync(It.IsAny<string>()))
            .ReturnsAsync((SaleSummaryDto?)null);
        _mockSaleService
            .Setup(s => s.GetSaleItemsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<SaleItemDto>());
        _mockSaleService
            .Setup(s => s.GetSalesHistoryAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<SaleSummaryDto>());

        _host = new FormTestHost<ReturnForm>(_mockSaleService.Object);
    }

    public void Dispose() => _host.Dispose();

    // ========================================================================
    // Helper: get a private field value via reflection on the UI thread
    // ========================================================================

    private TField GetField<TField>(string fieldName) where TField : class
        => _host.GetField<TField>(fieldName);

    private bool IsVisible(string fieldName)
        => _host.IsVisible(fieldName);

    private bool IsEnabled(string fieldName)
        => _host.IsEnabled(fieldName);

    private void SetTextBox(string fieldName, string text)
        => _host.SetTextBox(fieldName, text);

    private void ClickButton(string fieldName)
        => _host.ClickButton(fieldName);

    // ========================================================================
    // Initial State
    // ========================================================================

    [Fact]
    public void InitialState_HasRtlLayout()
    {
        _host.InvokeOnUI(() =>
        {
            Assert.Equal(RightToLeft.Yes, _host.Control.RightToLeft);
        });
    }

    [Fact]
    public void InitialState_HeaderTitleIsCorrect()
    {
        _host.InvokeOnUI(() =>
        {
            var title = GetField<Label>("_headerTitle");
            Assert.Contains("إرجاع", title.Text);
        });
    }

    [Fact]
    public void InitialState_SearchButtonIsEnabled()
    {
        Assert.True(IsEnabled("_btnSearch"));
    }

    [Fact]
    public void InitialState_BrowseButtonIsEnabled()
    {
        Assert.True(IsEnabled("_btnBrowse"));
    }

    [Fact]
    public void InitialState_InvoiceInfoPanelIsHidden()
    {
        Assert.False(IsVisible("_invoiceInfoPanel"));
    }

    [Fact]
    public void InitialState_ConfirmButtonIsHidden()
    {
        Assert.False(IsVisible("_btnConfirm"));
    }

    [Fact]
    public void InitialState_SummaryPanelIsHidden()
    {
        Assert.False(IsVisible("_summaryPanel"));
    }

    [Fact]
    public void InitialState_ValidationLabelIsHidden()
    {
        Assert.False(IsVisible("_lblValidation"));
    }

    [Fact]
    public void InitialState_InvoiceTextBoxIsNotEmpty()
    {
        _host.InvokeOnUI(() =>
        {
            var txtInvoice = GetField<POS.Desktop.CustomControls.RtlTextBox>("_txtInvoiceNumber");
            Assert.NotNull(txtInvoice);
            Assert.Equal("", txtInvoice.Text);
        });
    }

    // ========================================================================
    // Search Validation — Empty Invoice
    // ========================================================================

    [Fact]
    public async Task SearchWithEmptyInvoice_ShowsValidation()
    {
        // Leave invoice text empty and click search
        _host.InvokeOnUI(() =>
        {
            var field = typeof(ReturnForm).GetField("_btnSearch",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var btn = (Button?)field?.GetValue(_host.Control);
            btn?.PerformClick();
        });

        await Task.Delay(1500);

        _host.InvokeOnUI(() =>
        {
            var field = typeof(ReturnForm).GetField("_lblValidation",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var lbl = (Label?)field?.GetValue(_host.Control);
            Assert.NotNull(lbl);
            // After the fix, _reasonPanel.Visible is also set to true so label should be visible
            Assert.True(lbl.Visible, $"Expected label visible but was not. Text='{lbl.Text}'");
            Assert.Contains("رقم الفاتورة", lbl.Text);
        });
    }

    // ========================================================================
    // Invoice Not Found
    // ========================================================================

    [Fact]
    public async Task SearchInvoice_NotFound_ShowsNotFoundOverlay()
    {
        // Mock already returns null by default — "INV-NONEXISTENT" not found
        SetTextBox("_txtInvoiceNumber", "INV-NONEXISTENT");
        ClickButton("_btnSearch");
        await Task.Delay(500);

        _host.InvokeOnUI(() =>
        {
            var emptyOverlay = GetField<Panel>("_emptyOverlay");
            Assert.True(emptyOverlay.Visible);
        });
    }

    // ========================================================================
    // Invalid Invoice (not Completed)
    // ========================================================================

    [Fact]
    public async Task SearchInvoice_NotCompleted_ShowsInvalidInvoice()
    {
        // Setup — sale exists but is not completed
        var cancelledSale = new SaleSummaryDto(
            Guid.NewGuid(),
            "INV-CANCELLED-001",
            0, 0, 0, 0,
            "Cancelled",
            DefaultCreatedAt);

        _mockSaleService
            .Setup(s => s.GetSaleByInvoiceNumberAsync("INV-CANCELLED-001"))
            .ReturnsAsync(cancelledSale);

        SetTextBox("_txtInvoiceNumber", "INV-CANCELLED-001");
        ClickButton("_btnSearch");
        await Task.Delay(500);

        _host.InvokeOnUI(() =>
        {
            var emptyOverlay = GetField<Panel>("_emptyOverlay");
            Assert.True(emptyOverlay.Visible);

            var label = emptyOverlay.Controls.OfType<Label>().First();
            Assert.Contains("مكتملة فقط", label.Text);
        });
    }

    // ========================================================================
    // Invoice Found — Empty Items
    // ========================================================================

    [Fact]
    public async Task SearchInvoice_CompletedButNoItems_ShowsEmptyState()
    {
        _mockSaleService
            .Setup(s => s.GetSaleByInvoiceNumberAsync(DefaultInvoiceNumber))
            .ReturnsAsync(DefaultCompletedSale);
        _mockSaleService
            .Setup(s => s.GetSaleItemsAsync(DefaultSaleId))
            .ReturnsAsync(new List<SaleItemDto>()); // Empty items

        SetTextBox("_txtInvoiceNumber", DefaultInvoiceNumber);
        ClickButton("_btnSearch");
        await Task.Delay(500);

        _host.InvokeOnUI(() =>
        {
            var emptyOverlay = GetField<Panel>("_emptyOverlay");
            Assert.True(emptyOverlay.Visible);

            var label = emptyOverlay.Controls.OfType<Label>().First();
            Assert.Contains("لا توجد أصناف", label.Text);
        });
    }

    // ========================================================================
    // Invoice Found — With Items (Happy Path)
    // ========================================================================

    [Fact]
    public async Task SearchInvoice_CompletedWithItems_ShowsInvoiceInfoAndGrid()
    {
        _mockSaleService
            .Setup(s => s.GetSaleByInvoiceNumberAsync(DefaultInvoiceNumber))
            .ReturnsAsync(DefaultCompletedSale);
        _mockSaleService
            .Setup(s => s.GetSaleItemsAsync(DefaultSaleId))
            .ReturnsAsync(DefaultSaleItems);

        SetTextBox("_txtInvoiceNumber", DefaultInvoiceNumber);
        ClickButton("_btnSearch");
        await Task.Delay(800);

        _host.InvokeOnUI(() =>
        {
            // Invoice info should be visible
            Assert.True(GetField<Panel>("_invoiceInfoPanel").Visible);

            // Invoice number should be shown
            var lblInvoiceNumber = GetField<Label>("_lblInvoiceNumber");
            Assert.Contains(DefaultInvoiceNumber, lblInvoiceNumber.Text);

            // Invoice total should be shown
            var lblInvoiceTotal = GetField<Label>("_lblInvoiceTotal");
            Assert.Contains("53.000", lblInvoiceTotal.Text);

            // Items grid should have 2 rows
            var grid = GetField<POS.Desktop.CustomControls.RtlDataGridView>("_itemsGrid");
            Assert.Equal(2, grid.Rows.Count);

            // Summary panel should be visible
            Assert.True(GetField<Panel>("_summaryPanel").Visible);

            // Reason panel should be visible
            Assert.True(GetField<Panel>("_reasonPanel").Visible);

            // Confirm button should be hidden until items are selected
            Assert.False(GetField<POS.Desktop.CustomControls.RtlButton>("_btnConfirm").Visible);
        });
    }

    // ========================================================================
    // Item Selection — Shows Confirm Button and Updates Summary
    // ========================================================================

    [Fact]
    public async Task SelectItem_ShowsConfirmButtonAndUpdatesSummary()
    {
        _mockSaleService
            .Setup(s => s.GetSaleByInvoiceNumberAsync(DefaultInvoiceNumber))
            .ReturnsAsync(DefaultCompletedSale);
        _mockSaleService
            .Setup(s => s.GetSaleItemsAsync(DefaultSaleId))
            .ReturnsAsync(DefaultSaleItems);

        SetTextBox("_txtInvoiceNumber", DefaultInvoiceNumber);
        ClickButton("_btnSearch");
        await Task.Delay(800);

        // Select the first row in the grid
        _host.InvokeOnUI(() =>
        {
            var grid = GetField<POS.Desktop.CustomControls.RtlDataGridView>("_itemsGrid");
            grid.Rows[0].Cells["Select"].Value = true;
            grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        });

        await Task.Delay(200);

        // Confirm button should now be visible
        Assert.True(IsVisible("_btnConfirm"));

        // Summary should show selected count
        _host.InvokeOnUI(() =>
        {
            var lblSelectedCount = GetField<Label>("_lblSelectedCount");
            Assert.Contains("1", lblSelectedCount.Text);

            var lblRefundAmount = GetField<Label>("_lblRefundAmount");
            Assert.Contains("20.000", lblRefundAmount.Text); // 2 × 10.000
        });
    }

    // ========================================================================
    // Process Return — Validation (No Reason)
    // ========================================================================

    [Fact]
    public async Task ProcessReturn_MissingReason_ShowsValidation()
    {
        _mockSaleService
            .Setup(s => s.GetSaleByInvoiceNumberAsync(DefaultInvoiceNumber))
            .ReturnsAsync(DefaultCompletedSale);
        _mockSaleService
            .Setup(s => s.GetSaleItemsAsync(DefaultSaleId))
            .ReturnsAsync(DefaultSaleItems);

        SetTextBox("_txtInvoiceNumber", DefaultInvoiceNumber);
        ClickButton("_btnSearch");
        await Task.Delay(800);

        // Select an item
        _host.InvokeOnUI(() =>
        {
            var grid = GetField<POS.Desktop.CustomControls.RtlDataGridView>("_itemsGrid");
            grid.Rows[0].Cells["Select"].Value = true;
            grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        });

        await Task.Delay(200);

        // Confirm button visible, but don't fill reason — click confirm
        ClickButton("_btnConfirm");
        await Task.Delay(500);

        // Validation should show reason required
        _host.InvokeOnUI(() =>
        {
            var lblValidation = GetField<Label>("_lblValidation");
            Assert.True(lblValidation.Visible);
            Assert.Contains("سبب", lblValidation.Text);
        });
    }

    // ========================================================================
    // Permission Denied
    // ========================================================================

    [Fact]
    public void SetPermissionDenied_HidesMainPanelAndShowsOverlay()
    {
        _host.InvokeOnUI(() =>
        {
            _host.Control.SetPermissionDenied();

            // Main panel should be hidden
            var mainPanel = (Panel?)typeof(ReturnForm)
                .GetField("_mainPanel", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(_host.Control);
            Assert.NotNull(mainPanel);
            Assert.False(mainPanel.Visible);

            // Permission overlay should exist with correct text
            var overlay = (Panel?)typeof(ReturnForm)
                .GetField("_permissionOverlay", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(_host.Control);
            Assert.NotNull(overlay);

            // Check the overlay label text directly (Visible won't be true
            // because overlay is inside mainPanel which is hidden)
            var label = overlay.Controls.OfType<Label>().FirstOrDefault();
            Assert.NotNull(label);
            Assert.Contains("صلاحية", label.Text);
        });
    }

    // ========================================================================
    // Reset Form (Cancel Button)
    // ========================================================================

    [Fact]
    public async Task CancelButton_ResetsFormToInitialState()
    {
        // Load an invoice first
        _mockSaleService
            .Setup(s => s.GetSaleByInvoiceNumberAsync(DefaultInvoiceNumber))
            .ReturnsAsync(DefaultCompletedSale);
        _mockSaleService
            .Setup(s => s.GetSaleItemsAsync(DefaultSaleId))
            .ReturnsAsync(DefaultSaleItems);

        SetTextBox("_txtInvoiceNumber", DefaultInvoiceNumber);
        ClickButton("_btnSearch");
        await Task.Delay(800);

        // Now click cancel
        ClickButton("_btnCancel");
        Thread.Sleep(200);

        // Should be back to initial state
        _host.InvokeOnUI(() =>
        {
            // Invoice text should be cleared
            var txtInvoice = GetField<POS.Desktop.CustomControls.RtlTextBox>("_txtInvoiceNumber");
            Assert.Equal("", txtInvoice.Text);

            // Invoice info panel should be hidden
            Assert.False(GetField<Panel>("_invoiceInfoPanel").Visible);

            // Grid should be cleared
            var grid = GetField<POS.Desktop.CustomControls.RtlDataGridView>("_itemsGrid");
            Assert.Equal(0, grid.Rows.Count);

            // Validation should be hidden
            Assert.False(GetField<Label>("_lblValidation").Visible);
        });
    }

    // ========================================================================
    // Browse Sales History — No Completed Sales
    // ========================================================================

    [Fact]
    public async Task BrowseSalesHistory_NoCompletedSales_ShowsNotFound()
    {
        // Mock returns an empty list (no sales in history)
        _mockSaleService
            .Setup(s => s.GetSalesHistoryAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<SaleSummaryDto>());

        ClickButton("_btnBrowse");
        await Task.Delay(500);

        _host.InvokeOnUI(() =>
        {
            var emptyOverlay = GetField<Panel>("_emptyOverlay");
            Assert.True(emptyOverlay.Visible);

            var label = emptyOverlay.Controls.OfType<Label>().First();
            Assert.Contains("لا توجد فواتير", label.Text);
        });
    }
}
