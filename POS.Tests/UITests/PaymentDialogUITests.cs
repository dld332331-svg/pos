using Moq;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Desktop.Forms;
using Xunit;

namespace POS.Tests.UITests;

public sealed class PaymentDialogUITests : IDisposable
{
    private readonly Mock<ISaleService> _mockSaleService;
    private readonly FormTestHost<PaymentDialog> _host;
    private const decimal TotalDue = 45.500m;
    private static readonly Guid SaleId = Guid.NewGuid();

    public PaymentDialogUITests()
    {
        _mockSaleService = new Mock<ISaleService>(MockBehavior.Strict);
        _mockSaleService
            .Setup(s => s.ProcessPaymentAsync(It.IsAny<PaymentRequest>()))
            .ReturnsAsync(new PaymentResult(true, 0m, null));
        _host = new FormTestHost<PaymentDialog>(TotalDue, SaleId, _mockSaleService.Object);
    }

    public void Dispose() => _host.Dispose();

    // ========================================================================
    // Initial State
    // ========================================================================

    [Fact]
    public void InitialState_ShowsArabicTitle()
    {
        _host.InvokeOnUI(() =>
        {
            Assert.Equal("إتمام الدفع", _host.Control.Text);
        });
    }

    [Fact]
    public void InitialState_ShowsTotalDue()
    {
        _host.InvokeOnUI(() =>
        {
            var totalLabel = _host.GetField<Label>("_totalDueValueLabel");
            Assert.Contains("45", totalLabel.Text);
        });
    }

    [Fact]
    public void InitialState_ConfirmButtonIsEnabled()
    {
        Assert.True(_host.IsEnabled("_confirmButton"));
    }

    [Fact]
    public void InitialState_CashTabIsSelectedByDefault()
    {
        _host.InvokeOnUI(() =>
        {
            var combo = _host.GetField<ComboBox>("_methodCombo");
            Assert.Equal(0, combo.SelectedIndex);
        });
    }

    [Fact]
    public void InitialState_ChangeShowsZero()
    {
        _host.InvokeOnUI(() =>
        {
            var changeLabel = _host.GetField<Label>("_changeStatusLabel");
            Assert.Contains("0.000", changeLabel.Text);
        });
    }

    // ========================================================================
    // Cash Tab — Change Calculation
    // ========================================================================

    [Fact]
    public void CashTab_EnterExactAmount_ShowsExactChange()
    {
        _host.InvokeOnUI(() =>
        {
            var input = _host.GetField<NumericUpDown>("_amountReceivedInput");
            input.Value = TotalDue;
        });

        _host.InvokeOnUI(() =>
        {
            var changeLabel = _host.GetField<Label>("_changeStatusLabel");
            Assert.Contains("المبلغ تمام", changeLabel.Text);
        });
    }

    [Fact]
    public void CashTab_EnterMoreThanDue_ShowsPositiveChange()
    {
        _host.InvokeOnUI(() =>
        {
            var input = _host.GetField<NumericUpDown>("_amountReceivedInput");
            input.Value = 100m;
        });

        _host.InvokeOnUI(() =>
        {
            var changeLabel = _host.GetField<Label>("_changeStatusLabel");
            Assert.Contains("الباقي", changeLabel.Text);
            Assert.Contains("54", changeLabel.Text);
        });
    }

    [Fact]
    public void CashTab_EnterLessThanDue_ShowsRemaining()
    {
        _host.InvokeOnUI(() =>
        {
            var input = _host.GetField<NumericUpDown>("_amountReceivedInput");
            input.Value = 30m;
        });

        _host.InvokeOnUI(() =>
        {
            var changeLabel = _host.GetField<Label>("_changeStatusLabel");
            Assert.Contains("متبقي", changeLabel.Text);
        });
    }

    [Fact]
    public void CashTab_QuickAmountButton_SetsAmount()
    {
        _host.InvokeOnUI(() =>
        {
            // First quick amount button should be 5 JOD
            var flowPanel = _host.GetField<FlowLayoutPanel>("_quickAmountsPanel");
            var firstBtn = flowPanel.Controls.OfType<Button>().First();
            firstBtn.PerformClick();
        });

        var value = _host.GetNumericUpDownValue("_amountReceivedInput");
        Assert.Equal(5m, value);
    }

    // ========================================================================
    // Card Tab
    // ========================================================================

    [Fact]
    public void CardTab_ShowsTotalAmount()
    {
        _host.InvokeOnUI(() =>
        {
            var combo = _host.GetField<ComboBox>("_methodCombo");
            combo.SelectedIndex = 1; // Switch to Card
        });

        _host.InvokeOnUI(() =>
        {
            var cardAmountLabel = _host.GetField<Label>("_paymentAmountValueLabel");
            Assert.Contains("45", cardAmountLabel.Text);
        });
    }

    // ========================================================================
    // Validation — Insufficient Amount
    // ========================================================================

    [Fact]
    public void CashTab_ConfirmWithLessAmount_ShowsInvalidState()
    {
        _host.InvokeOnUI(() =>
        {
            var input = _host.GetField<NumericUpDown>("_amountReceivedInput");
            input.Value = 10m;
        });

        _host.ClickButton("_confirmButton");
        Thread.Sleep(300);

        _host.InvokeOnUI(() =>
        {
            var statusLabel = _host.GetField<Label>("_statusLabel");
            Assert.True(statusLabel.Visible);
            Assert.Contains("أقل", statusLabel.Text);
        });
    }

    // ========================================================================
    // Payment Success
    // ========================================================================

    [Fact]
    public async Task CashTab_ConfirmWithExactAmount_Succeeds()
    {
        _host.InvokeOnUI(() =>
        {
            var input = _host.GetField<NumericUpDown>("_amountReceivedInput");
            input.Value = TotalDue;
        });

        var succeedTask = _host.AwaitSimpleEvent("PaymentSucceeded");
        _host.ClickButton("_confirmButton");

        var fired = await succeedTask;
        Assert.True(fired, "PaymentSucceeded event should fire after confirmation.");
    }

    // ========================================================================
    // Cancel
    // ========================================================================

    [Fact]
    public async Task CancelButton_FiresPaymentCancelled()
    {
        var cancelTask = _host.AwaitSimpleEvent("PaymentCancelled");
        _host.ClickButton("_cancelButton");

        var fired = await cancelTask;
        Assert.True(fired, "PaymentCancelled event should fire.");
    }

    // ========================================================================
    // Public API Tests
    // ========================================================================

    [Fact]
    public void ShowSuccess_SetsSuccessState()
    {
        _host.InvokeOnUI(() =>
        {
            var dlg = _host.Control;
            dlg.ShowSuccess(5.500m);
        });

        _host.InvokeOnUI(() =>
        {
            var changeLabel = _host.GetField<Label>("_changeStatusLabel");
            Assert.Contains("5", changeLabel.Text);
        });
    }

    [Fact]
    public void ShowFailure_SetsFailureState()
    {
        _host.InvokeOnUI(() =>
        {
            var dlg = _host.Control;
            dlg.ShowFailure("فشلت عملية الدفع");
        });

        _host.InvokeOnUI(() =>
        {
            var statusLabel = _host.GetField<Label>("_statusLabel");
            Assert.Contains("فشلت", statusLabel.Text);
        });
    }
}
