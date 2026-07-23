using System.Reflection;
using System.Windows.Forms;
using Moq;
using Xunit;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Desktop.Forms;

namespace POS.Tests.UITests;

/// <summary>
/// End-to-end UI tests for PosTerminalForm (POS-001).
/// Tests all states: EmptySale, ActiveSale, keyboard handling, public API.
/// </summary>
public sealed class PosTerminalFormUITests : IDisposable
{
    private readonly Mock<ISaleService> _mockSaleService;
    private readonly Mock<IProductService> _mockProductService;
    private readonly Mock<IPrinterManagementService> _mockPrinterService;
    private readonly FormTestHost<PosTerminalForm> _host;
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ShiftId = Guid.NewGuid();

    public PosTerminalFormUITests()
    {
        _mockSaleService = new Mock<ISaleService>(MockBehavior.Strict);
        _mockProductService = new Mock<IProductService>(MockBehavior.Strict);
        _mockPrinterService = new Mock<IPrinterManagementService>(MockBehavior.Strict);

        // Setup default mock behaviors
        _mockSaleService
            .Setup(s => s.CreateNewSaleAsync(UserId, ShiftId))
            .ReturnsAsync(Guid.NewGuid());
        _mockSaleService
            .Setup(s => s.AddItemAsync(It.IsAny<Guid>(), It.IsAny<AddItemRequest>()))
            .Returns(Task.CompletedTask);
        _mockSaleService
            .Setup(s => s.GetHeldSalesAsync(ShiftId))
            .ReturnsAsync(new List<HeldSaleDto>());

        _mockProductService
            .Setup(p => p.GetCategoriesAsync())
            .ReturnsAsync(new List<CategoryDto>());
        _mockProductService
            .Setup(p => p.GetProductsAsync(It.IsAny<ProductFilterDto>()))
            .ReturnsAsync(new PagedResult<ProductDto>(new List<ProductDto>(), 0, 1, 200));

        // Setup printer service to succeed so OnPaymentSuccess doesn't trigger PrinterFailure
        _mockPrinterService
            .Setup(p => p.PrintReceiptAsync(It.IsAny<Guid>()))
            .ReturnsAsync(true);
        _mockPrinterService
            .Setup(p => p.PrintKitchenTicketsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(true);

        _host = new FormTestHost<PosTerminalForm>(
            _mockSaleService.Object, _mockProductService.Object,
            _mockPrinterService.Object, UserId, ShiftId);
    }

    public void Dispose() => _host.Dispose();

    // ========================================================================
    // Helper: Set ActiveSale state via reflection (since PosState is private)
    // ========================================================================

    private void SetActiveSaleState()
    {
        _host.InvokeOnUI(() =>
        {
            // PosState.ActiveSale = 1 (enum values: EmptySale=0, ActiveSale=1)
            var stateField = typeof(PosTerminalForm).GetField("_currentState",
                BindingFlags.NonPublic | BindingFlags.Instance);
            stateField?.SetValue(_host.Control, (Enum)Enum.ToObject(
                stateField.FieldType, 1));

            var saleIdField = typeof(PosTerminalForm).GetField("_currentSaleId",
                BindingFlags.NonPublic | BindingFlags.Instance);
            saleIdField?.SetValue(_host.Control, Guid.NewGuid());

            var itemsField = typeof(PosTerminalForm).GetField("_saleItems",
                BindingFlags.NonPublic | BindingFlags.Instance);
            itemsField?.SetValue(_host.Control, new List<SaleItemDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "قهوة", 1, 1.500m, 0, 16, 0.240m, 1.740m, 0.750m, null, null)
            });
        });
    }

    // ========================================================================
    // Initial State — Empty Sale
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
    public void InitialState_InvoiceHeaderShowsNewInvoice()
    {
        _host.InvokeOnUI(() =>
        {
            var header = _host.GetField<Label>("_invoiceNumberLabel");
            Assert.Contains("فاتورة جديدة", header.Text);
        });
    }

    [Fact]
    public void InitialState_InvoiceStatusIsNew()
    {
        _host.InvokeOnUI(() =>
        {
            var status = _host.GetField<Label>("_invoiceStatusLabel");
            Assert.Contains("جديد", status.Text);
        });
    }

    [Fact]
    public void InitialState_ItemsCountIsZero()
    {
        _host.InvokeOnUI(() =>
        {
            var countLabel = _host.GetField<Label>("_invoiceItemsCountLabel");
            Assert.Contains("0", countLabel.Text);
        });
    }

    [Fact]
    public void InitialState_StatusBarShowsReady()
    {
        var text = _host.GetText("_statusBarLabel");
        Assert.Contains("جاهز", text);
    }

    [Fact]
    public void InitialState_PaymentButtonsAreDisabled()
    {
        Assert.False(_host.IsEnabled("_cashPaymentButton"));
        Assert.False(_host.IsEnabled("_cardPaymentButton"));
    }

    [Fact]
    public void InitialState_HoldAndCancelButtonsAreDisabled()
    {
        Assert.False(_host.IsEnabled("_holdButton"));
        Assert.False(_host.IsEnabled("_cancelButton"));
    }

    [Fact]
    public void InitialState_DiscountButtonIsDisabled()
    {
        Assert.False(_host.IsEnabled("_discountButton"));
    }

    [Fact]
    public void InitialState_SearchBoxIsEnabled()
    {
        Assert.True(_host.IsEnabled("_searchTextBox"));
    }

    // ========================================================================
    // Totals Display
    // ========================================================================

    [Fact]
    public void InitialState_SubtotalIsZero()
    {
        _host.InvokeOnUI(() =>
        {
            var subtotal = _host.GetField<Label>("_subtotalLabel");
            Assert.Contains("0.000", subtotal.Text);
        });
    }

    [Fact]
    public void InitialState_TotalIsZero()
    {
        _host.InvokeOnUI(() =>
        {
            var total = _host.GetField<Label>("_totalLabel");
            Assert.Contains("0.000", total.Text);
        });
    }

    // ========================================================================
    // Category Loading (via Public API — uses InvokeAsync for thread safety)
    // ========================================================================

    [Fact]
    public async Task LoadCategoriesAsync_PopulatesCategoryFlow()
    {
        await _host.InvokeAsync(() => _host.Control.LoadCategoriesAsync());

        _host.InvokeOnUI(() =>
        {
            var flowLayout = _host.GetField<FlowLayoutPanel>("_categoryFlowLayout");
            Assert.NotEmpty(flowLayout.Controls.OfType<Button>());
        });
    }

    // ========================================================================
    // Product Loading (via Public API — uses InvokeAsync for thread safety)
    // ========================================================================

    [Fact]
    public async Task LoadProductsAsync_WithoutService_LoadsSampleData()
    {
        await _host.InvokeAsync(() => _host.Control.LoadProductsAsync());

        _host.InvokeOnUI(() =>
        {
            var productGrid = _host.GetField<FlowLayoutPanel>("_productGrid");
            Assert.True(productGrid.Controls.Count > 0);
        });
    }

    // ========================================================================
    // Product Search
    // ========================================================================

    [Fact]
    public void SearchTextBox_PlaceholderContainsArabic()
    {
        _host.InvokeOnUI(() =>
        {
            var searchBox = _host.GetField<TextBox>("_searchTextBox");
            Assert.Contains("بحث", searchBox.PlaceholderText);
        });
    }

    // ========================================================================
    // Keyboard Shortcut — F8 Focuses Search
    // ========================================================================

    [Fact]
    public void F8Key_FocusesSearchBox()
    {
        _host.InvokeOnUI(() =>
        {
            _host.Control.HandleKeyDown(new KeyEventArgs(Keys.F8));
            var searchBox = _host.GetField<TextBox>("_searchTextBox");
            Assert.True(searchBox.Focused);
        });
    }

    // ========================================================================
    // Keyboard Shortcut — Escape Dismisses Overlay
    // ========================================================================

    [Fact]
    public void EscapeKey_DismissesOverlay()
    {
        _host.InvokeOnUI(() =>
        {
            var overlayField = typeof(PosTerminalForm)
                .GetField("_overlayPanel", BindingFlags.NonPublic | BindingFlags.Instance);
            var overlay = overlayField?.GetValue(_host.Control) as Panel;
            if (overlay != null)
                overlay.Visible = true;

            _host.Control.HandleKeyDown(new KeyEventArgs(Keys.Escape));

            Assert.False(overlay?.Visible ?? true);
        });
    }

    // ========================================================================
    // Payment Initiation (from ActiveSale state via F2)
    // ========================================================================

    [Fact]
    public void F2Key_InActiveSale_RaisesPaymentEvent()
    {
        SetActiveSaleState();

        var eventFired = false;
        _host.Control.RequestPayment += (s, e) => eventFired = true;

        _host.InvokeOnUI(() =>
        {
            _host.Control.HandleKeyDown(new KeyEventArgs(Keys.F2));
        });

        Assert.True(eventFired, "RequestPayment event should fire with F2 in ActiveSale.");
    }

    // ========================================================================
    // Cash Button Click (from ActiveSale) — calls InitiatePayment directly
    // ========================================================================

    [Fact]
    public void CashPaymentButton_WhenActive_RaisesRequestPayment()
    {
        SetActiveSaleState();

        var eventFired = false;
        _host.Control.RequestPayment += (s, e) => eventFired = true;

        // Call InitiatePayment("Cash") directly on the UI thread via reflection.
        // This tests the same payment-initiation logic that the button Click handler
        // (s, e) => InitiatePayment("Cash") would invoke via PerformClick().
        _host.InvokeOnUI(() =>
        {
            var method = typeof(PosTerminalForm).GetMethod("InitiatePaymentAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(_host.Control, new object[] { "Cash" });
        });

        Assert.True(eventFired, "RequestPayment event should fire from InitiatePayment in ActiveSale.");
    }

    // ========================================================================
    // Public API — ClearCurrentSale
    // ========================================================================

    [Fact]
    public void ClearCurrentSale_ResetsToEmptyState()
    {
        _host.InvokeOnUI(() =>
        {
            var saleIdField = typeof(PosTerminalForm)
                .GetField("_currentSaleId", BindingFlags.NonPublic | BindingFlags.Instance);
            saleIdField?.SetValue(_host.Control, Guid.NewGuid());

            _host.Control.ClearCurrentSale();
        });

        Assert.False(_host.IsEnabled("_cashPaymentButton"));

        _host.InvokeOnUI(() =>
        {
            var header = _host.GetField<Label>("_invoiceNumberLabel");
            Assert.Contains("فاتورة جديدة", header.Text);
        });
    }

    // ========================================================================
    // Public API — UpdateRetrieveButton
    // ========================================================================

    [Fact]
    public void UpdateRetrieveButton_WithCount_ShowsBadge()
    {
        _host.InvokeOnUI(() => _host.Control.UpdateRetrieveButton(3));

        _host.InvokeOnUI(() =>
        {
            var retrieveBtn = _host.GetField<Button>("_retrieveButton");
            Assert.Contains("3", retrieveBtn.Text);
        });
    }

    [Fact]
    public void UpdateRetrieveButton_WithZero_ShowsDefault()
    {
        _host.InvokeOnUI(() => _host.Control.UpdateRetrieveButton(0));

        _host.InvokeOnUI(() =>
        {
            var retrieveBtn = _host.GetField<Button>("_retrieveButton");
            Assert.DoesNotContain("0", retrieveBtn.Text);
        });
    }

    // ========================================================================
    // Public API — OnPaymentSuccess
    // ========================================================================

    [Fact]
    public void OnPaymentSuccess_ShowsSuccessState()
    {
        _host.InvokeOnUI(() =>
        {
            var saleIdField = typeof(PosTerminalForm)
                .GetField("_currentSaleId", BindingFlags.NonPublic | BindingFlags.Instance);
            saleIdField?.SetValue(_host.Control, Guid.NewGuid());

            _host.Control.OnPaymentSuccess(5.500m);
        });

        var text = _host.GetText("_statusBarLabel");
        Assert.Contains("تمت طباعة", text);
    }
}
