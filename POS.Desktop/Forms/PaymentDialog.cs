using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Desktop.Forms;

public sealed class PaymentDialog : Form
{
    private enum PaymentState
    {
        EnterAmount,
        ExactChange,
        ChangeDue,
        Insufficient,
        Processing,
        Complete,
        Error
    }

    private const string MethodCash = "نقداً";
    private const string MethodCard = "بطاقة";
    private const string MethodEWallet = "محفظة إلكترونية";
    private const string MethodCredit = "آجل";

    private readonly ISaleService? _saleService;
    private readonly ICustomerService? _customerService;
    private PaymentState _currentState = PaymentState.EnterAmount;
    private decimal _totalDue;
    private Guid _saleId;
    private CustomerDto? _selectedCustomer;

    // Layout containers
    private Panel _mainPanel;
    private Label _titleLabel;
    private Label _totalDueLabel;
    private Label _totalDueValueLabel;

    // Payment method selector
    private ComboBox _methodCombo;
    private ComboBox _customerCombo;
    private Label _customerLabel;
    private Panel _contentPanel;

    // Cash controls
    private NumericUpDown _amountReceivedInput;
    private FlowLayoutPanel _quickAmountsPanel;
    private Label _changeStatusLabel;

    // Card / E-Wallet controls (shared)
    private Label _paymentAmountValueLabel;
    private Label _paymentAmountCaptionLabel;
    private TextBox _referenceTextBox;
    private Label _referenceLabel;
    private Label _paymentNoteLabel;

    // Credit controls
    private Label _creditAmountValueLabel;
    private Label _creditNoteLabel;

    // Action controls
    private Button _confirmButton;
    private Button _cancelButton;
    private Label _statusLabel;
    private Panel _processingPanel;
    private Panel _successPanel;
    private Panel _failurePanel;

    public event EventHandler<PaymentRequest>? PaymentConfirmed;
    public event EventHandler? PaymentCancelled;
    public event EventHandler? PaymentSucceeded;

    public decimal ChangeAmount { get; private set; }
    public decimal AmountReceived { get; private set; }

    public PaymentDialog(decimal totalDue, Guid saleId)
    {
        _totalDue = totalDue;
        _saleId = saleId;
        InitializeComponent();
    }

    public PaymentDialog(decimal totalDue, Guid saleId, ISaleService saleService)
        : this(totalDue, saleId)
    {
        _saleService = saleService;
    }

    public PaymentDialog(decimal totalDue, Guid saleId, ISaleService saleService, ICustomerService customerService)
        : this(totalDue, saleId)
    {
        _saleService = saleService;
        _customerService = customerService;
    }

    private void InitializeComponent()
    {
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Text = "إتمام الدفع";
        ClientSize = new Size(440, 580);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = DesignTokens.BackgroundColor;
        Font = DesignTokens.DefaultFont;

        _mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingLG)
        };

        // Title
        _titleLabel = new Label
        {
            Text = "إتمام عملية الدفع",
            Font = DesignTokens.HeadingFont,
            ForeColor = DesignTokens.PrimaryColor,
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter
        };

        // Total due section
        var totalDuePanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 55,
            BackColor = DesignTokens.CardColor,
            Padding = new Padding(DesignTokens.SpacingMD)
        };

        _totalDueLabel = new Label
        {
            Text = "المبلغ المستحق",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            Dock = DockStyle.Right,
            Width = 200,
            Height = 55,
            TextAlign = ContentAlignment.MiddleRight
        };

        _totalDueValueLabel = new Label
        {
            Text = $"{_totalDue:N3} JOD",
            Font = new Font(DesignTokens.DefaultFont.FontFamily, 20f, FontStyle.Bold),
            ForeColor = DesignTokens.PrimaryColor,
            Dock = DockStyle.Fill,
            Height = 55,
            TextAlign = ContentAlignment.MiddleCenter
        };

        totalDuePanel.Controls.Add(_totalDueValueLabel);
        totalDuePanel.Controls.Add(_totalDueLabel);

        // ── Payment method selector panel ──────────────────────────────
        var selectorPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            Padding = new Padding(0, DesignTokens.SpacingSM, 0, DesignTokens.SpacingSM)
        };

        var methodLabel = new Label
        {
            Text = "طريقة الدفع:",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextPrimaryColor,
            Location = new Point(200, 5),
            Size = new Size(180, 25),
            TextAlign = ContentAlignment.MiddleRight
        };

        _methodCombo = new ComboBox
        {
            Location = new Point(10, 5),
            Size = new Size(180, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = DesignTokens.DefaultFont,
            RightToLeft = RightToLeft.Yes
        };
        _methodCombo.Items.AddRange(new[] { MethodCash, MethodCard, MethodEWallet, MethodCredit });
        _methodCombo.SelectedIndex = 0;
        _methodCombo.SelectedIndexChanged += (s, e) => OnPaymentMethodChanged();

        selectorPanel.Controls.Add(methodLabel);
        selectorPanel.Controls.Add(_methodCombo);

        // ── Customer selector (visible only for credit) ────────────────
        _customerLabel = new Label
        {
            Text = "اختر العميل:",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextPrimaryColor,
            Location = new Point(200, 42),
            Size = new Size(180, 25),
            TextAlign = ContentAlignment.MiddleRight,
            Visible = false
        };

        _customerCombo = new ComboBox
        {
            Location = new Point(10, 42),
            Size = new Size(180, 28),
            DropDownStyle = ComboBoxStyle.DropDown,
            Font = DesignTokens.DefaultFont,
            RightToLeft = RightToLeft.Yes,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems,
            DisplayMember = "DisplayText",
            ValueMember = "Self",
            Visible = false
        };
        _customerCombo.SelectedIndexChanged += (s, e) =>
        {
            _selectedCustomer = _customerCombo.SelectedItem as CustomerDto;
        };

        selectorPanel.Controls.Add(_customerLabel);
        selectorPanel.Controls.Add(_customerCombo);

        // ── Content panel (dynamic area) ───────────────────────────────
        _contentPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 180,
        };

        // Cash content
        _amountReceivedInput = new NumericUpDown
        {
            Location = new Point(10, 10),
            Size = new Size(180, 28),
            Font = new Font(DesignTokens.DefaultFont.FontFamily, 12f),
            DecimalPlaces = 3,
            Minimum = 0,
            Maximum = 999999,
            ThousandsSeparator = true,
            RightToLeft = RightToLeft.Yes,
            TextAlign = HorizontalAlignment.Center
        };
        _amountReceivedInput.ValueChanged += (s, e) => OnAmountChanged();

        var amountReceivedLabel = new Label
        {
            Text = "المبلغ المستلم:",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextPrimaryColor,
            Location = new Point(200, 10),
            Size = new Size(180, 25),
            TextAlign = ContentAlignment.MiddleRight
        };

        _quickAmountsPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            Location = new Point(10, 50),
            Size = new Size(380, 80),
            BackColor = DesignTokens.SurfaceColor
        };
        var quickAmounts = new[] { 5m, 10m, 20m, 50m, 100m };
        foreach (var amt in quickAmounts)
        {
            var btn = new Button
            {
                Text = $"{amt:N0}",
                Font = DesignTokens.DefaultFont,
                Size = new Size(110, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = DesignTokens.CardColor,
                Cursor = Cursors.Hand,
                Margin = new Padding(DesignTokens.SpacingXS),
                Tag = amt
            };
            btn.Click += (s, e) => { _amountReceivedInput.Value = (decimal)((Button)s!).Tag!; OnAmountChanged(); };
            _quickAmountsPanel.Controls.Add(btn);
        }

        _changeStatusLabel = new Label
        {
            Text = "0.000 JOD",
            Font = new Font(DesignTokens.DefaultFont.FontFamily, 16f, FontStyle.Bold),
            ForeColor = DesignTokens.SuccessColor,
            Location = new Point(10, 135),
            Size = new Size(380, 35),
            TextAlign = ContentAlignment.MiddleCenter
        };

        _contentPanel.Controls.Add(amountReceivedLabel);
        _contentPanel.Controls.Add(_amountReceivedInput);
        _contentPanel.Controls.Add(_quickAmountsPanel);
        _contentPanel.Controls.Add(_changeStatusLabel);

        // Card / E-Wallet controls (hidden initially)
        _paymentAmountCaptionLabel = new Label
        {
            Text = "المبلغ:",
            Font = DesignTokens.SubheadingFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            Location = new Point(200, 20),
            Size = new Size(180, 30),
            TextAlign = ContentAlignment.MiddleRight,
            Visible = false
        };

        _paymentAmountValueLabel = new Label
        {
            Text = $"{_totalDue:N3} JOD",
            Font = new Font(DesignTokens.DefaultFont.FontFamily, 18f, FontStyle.Bold),
            ForeColor = DesignTokens.PrimaryColor,
            Location = new Point(10, 15),
            Size = new Size(180, 35),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };

        _referenceLabel = new Label
        {
            Text = "رقم المرجع (اختياري):",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextPrimaryColor,
            Location = new Point(200, 70),
            Size = new Size(180, 25),
            TextAlign = ContentAlignment.MiddleRight,
            Visible = false
        };

        _referenceTextBox = new TextBox
        {
            Location = new Point(10, 70),
            Size = new Size(180, 28),
            Font = DesignTokens.DefaultFont,
            RightToLeft = RightToLeft.Yes,
            PlaceholderText = "أدخل رقم المرجع",
            Visible = false
        };

        _paymentNoteLabel = new Label
        {
            Text = "يرجى إدخال المبلغ على جهاز الدفع ثم الضغط على تأكيد",
            Font = DesignTokens.SmallFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            Location = new Point(10, 120),
            Size = new Size(380, 40),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };

        _contentPanel.Controls.Add(_paymentAmountCaptionLabel);
        _contentPanel.Controls.Add(_paymentAmountValueLabel);
        _contentPanel.Controls.Add(_referenceLabel);
        _contentPanel.Controls.Add(_referenceTextBox);
        _contentPanel.Controls.Add(_paymentNoteLabel);

        // Credit controls (hidden initially)
        _creditAmountValueLabel = new Label
        {
            Text = $"{_totalDue:N3} JOD",
            Font = new Font(DesignTokens.DefaultFont.FontFamily, 18f, FontStyle.Bold),
            ForeColor = DesignTokens.PrimaryColor,
            Location = new Point(10, 15),
            Size = new Size(380, 35),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };

        _creditNoteLabel = new Label
        {
            Text = "يرجى اختيار عميل لإتمام عملية البيع الآجل",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            Location = new Point(10, 70),
            Size = new Size(380, 40),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };

        _contentPanel.Controls.Add(_creditAmountValueLabel);
        _contentPanel.Controls.Add(_creditNoteLabel);

        // Status label
        _statusLabel = new Label
        {
            Text = "",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.ErrorColor,
            Dock = DockStyle.Top,
            Height = 25,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };

        // Processing overlay
        _processingPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(200, DesignTokens.SurfaceColor),
            Visible = false
        };
        var procLabel = new Label
        {
            Text = "جاري معالجة الدفع...",
            Font = DesignTokens.SubheadingFont,
            ForeColor = DesignTokens.PrimaryColor,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        _processingPanel.Controls.Add(procLabel);

        // Success panel
        _successPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(200, DesignTokens.SurfaceColor),
            Visible = false
        };
        var successIcon = new Label { Text = "✅", Font = new Font("Segoe UI Emoji", 48), Dock = DockStyle.Top, Height = 80, TextAlign = ContentAlignment.MiddleCenter };
        var successMsg = new Label { Text = "تمت عملية الدفع بنجاح!", Font = DesignTokens.HeadingFont, ForeColor = DesignTokens.SuccessColor, Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter };
        _successPanel.Controls.Add(successMsg);
        _successPanel.Controls.Add(successIcon);

        // Failure panel
        _failurePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(200, DesignTokens.SurfaceColor),
            Visible = false
        };
        var failIcon = new Label { Text = "❌", Font = new Font("Segoe UI Emoji", 48), Dock = DockStyle.Top, Height = 80, TextAlign = ContentAlignment.MiddleCenter };
        var failMsg = new Label { Text = "فشلت عملية الدفع", Font = DesignTokens.HeadingFont, ForeColor = DesignTokens.ErrorColor, Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter };
        var failRetryBtn = new Button { Text = "إعادة المحاولة", Font = DesignTokens.ButtonFont, BackColor = DesignTokens.PrimaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Size = new Size(150, 40), Dock = DockStyle.Bottom, Height = 50, Cursor = Cursors.Hand };
        failRetryBtn.Click += (s, e) => SetState(PaymentState.EnterAmount);
        _failurePanel.Controls.Add(failRetryBtn);
        _failurePanel.Controls.Add(failMsg);
        _failurePanel.Controls.Add(failIcon);

        // Action buttons
        var actionsPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(DesignTokens.SpacingSM)
        };

        _confirmButton = new Button
        {
            Text = "تأكيد الدفع",
            Font = DesignTokens.ButtonFont,
            ForeColor = Color.White,
            BackColor = DesignTokens.SuccessColor,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(180, 40),
            Dock = DockStyle.Left,
            Cursor = Cursors.Hand
        };

        _cancelButton = new Button
        {
            Text = "إلغاء",
            Font = DesignTokens.ButtonFont,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(180, 40),
            Dock = DockStyle.Right,
            Cursor = Cursors.Hand,
            BackColor = DesignTokens.BorderColor,
            ForeColor = DesignTokens.TextPrimaryColor
        };

        actionsPanel.Controls.Add(_confirmButton);
        actionsPanel.Controls.Add(_cancelButton);

        // Assemble
        _mainPanel.Controls.Add(_failurePanel);
        _mainPanel.Controls.Add(_successPanel);
        _mainPanel.Controls.Add(_processingPanel);
        _mainPanel.Controls.Add(_statusLabel);
        _mainPanel.Controls.Add(_contentPanel);
        _mainPanel.Controls.Add(selectorPanel);
        _mainPanel.Controls.Add(totalDuePanel);
        _mainPanel.Controls.Add(_titleLabel);

        Controls.Add(_mainPanel);
        Controls.Add(actionsPanel);

        // Events
        _confirmButton.Click += async (s, e) => await ProcessPaymentAsync();
        _cancelButton.Click += (s, e) =>
        {
            PaymentCancelled?.Invoke(this, EventArgs.Empty);
            Close();
        };
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape) { PaymentCancelled?.Invoke(this, EventArgs.Empty); Close(); }
        };
        Load += async (s, e) =>
        {
            _amountReceivedInput.Focus();
            _amountReceivedInput.Select(0, _amountReceivedInput.Text.Length);
            await LoadCustomersAsync();
        };
        AcceptButton = _confirmButton;
    }

    private void OnPaymentMethodChanged()
    {
        var method = _methodCombo.SelectedItem?.ToString();

        // Hide all content groups
        _amountReceivedInput.Visible = false;
                SetAmountReceivedLabelVisible(false);
        _quickAmountsPanel.Visible = false;
        _changeStatusLabel.Visible = false;
        _paymentAmountCaptionLabel.Visible = false;
        _paymentAmountValueLabel.Visible = false;
        _referenceLabel.Visible = false;
        _referenceTextBox.Visible = false;
        _paymentNoteLabel.Visible = false;
        _customerLabel.Visible = false;
        _customerCombo.Visible = false;
        _creditAmountValueLabel.Visible = false;
        _creditNoteLabel.Visible = false;

        switch (method)
        {
            case MethodCash:
                _amountReceivedInput.Visible = true;
                SetAmountReceivedLabelVisible(true);
                _quickAmountsPanel.Visible = true;
                _changeStatusLabel.Visible = true;
                _amountReceivedInput.Focus();
                OnAmountChanged();
                break;

            case MethodCard:
            case MethodEWallet:
                _paymentAmountCaptionLabel.Text = method == MethodCard ? "المبلغ (بطاقة):" : "المبلغ (محفظة إلكترونية):";
                _paymentAmountCaptionLabel.Visible = true;
                _paymentAmountValueLabel.Visible = true;
                _referenceLabel.Visible = true;
                _referenceTextBox.Visible = true;
                if (method == MethodCard)
                    _paymentNoteLabel.Text = "يرجى إدخال المبلغ على جهاز الدفع ثم الضغط على تأكيد";
                else
                    _paymentNoteLabel.Text = "يرجى تأكيد الدفع عبر المحفظة الإلكترونية";
                _paymentNoteLabel.Visible = true;
                _referenceTextBox.Focus();
                _changeStatusLabel.Visible = false;
                SetState(PaymentState.EnterAmount);
                break;

            case MethodCredit:
                _creditAmountValueLabel.Visible = true;
                _creditNoteLabel.Visible = true;
                _customerLabel.Visible = true;
                _customerCombo.Visible = true;
                _customerCombo.Focus();
                _changeStatusLabel.Visible = false;
                SetState(PaymentState.EnterAmount);
                break;
        }
    }

    private void SetAmountReceivedLabelVisible(bool visible)
    {
        foreach (Control c in _contentPanel.Controls)
        {
            if (c is Label lbl && lbl.Text == "المبلغ المستلم:")
            {
                lbl.Visible = visible;
                break;
            }
        }
    }

    private void OnAmountChanged()
    {
        if (_methodCombo.SelectedItem?.ToString() != MethodCash)
            return;

        var received = _amountReceivedInput.Value;
        var diff = received - _totalDue;

        if (diff == 0)
        {
            _changeStatusLabel.Text = "المبلغ تمام";
            _changeStatusLabel.ForeColor = DesignTokens.SuccessColor;
            SetState(PaymentState.ExactChange);
        }
        else if (diff > 0)
        {
            _changeStatusLabel.Text = $"الباقي: {diff:N3} JOD";
            _changeStatusLabel.ForeColor = DesignTokens.SuccessColor;
            SetState(PaymentState.ChangeDue);
        }
        else
        {
            _changeStatusLabel.Text = $"متبقي: {Math.Abs(diff):N3} JOD";
            _changeStatusLabel.ForeColor = DesignTokens.ErrorColor;
            SetState(PaymentState.Insufficient);
        }
    }

    private void SetState(PaymentState state)
    {
        _currentState = state;

        _processingPanel.Visible = state == PaymentState.Processing;
        _successPanel.Visible = state == PaymentState.Complete;
        _failurePanel.Visible = state == PaymentState.Error;
        _statusLabel.Visible = state == PaymentState.Insufficient;

        var isTerminal = state == PaymentState.Complete || state == PaymentState.Error;
        _contentPanel.Enabled = !isTerminal;
        _methodCombo.Enabled = state == PaymentState.EnterAmount || state == PaymentState.ExactChange || state == PaymentState.ChangeDue || state == PaymentState.Insufficient;
        _confirmButton.Enabled = state != PaymentState.Processing && state != PaymentState.Complete;
        _cancelButton.Visible = state != PaymentState.Complete;

        if (state == PaymentState.Complete)
        {
            _cancelButton.Text = "إغلاق";
            _cancelButton.Visible = true;
            _confirmButton.Visible = false;
        }

        if (state == PaymentState.EnterAmount)
        {
            _statusLabel.Text = "";
            _statusLabel.Visible = false;
            _confirmButton.Visible = true;
            _cancelButton.Text = "إلغاء";
        }
    }

    private async Task LoadCustomersAsync()
    {
        if (_customerService == null) return;

        try
        {
            var customers = await _customerService.GetCustomersAsync();
            _customerCombo.Items.Clear();
            if (customers.Count > 0)
            {
                _customerCombo.Items.AddRange(customers.ToArray());
                _customerCombo.SelectedIndex = -1;
            }
        }
        catch
        {
            // Silently handle - customer loading is non-critical
        }
    }

    private async Task ProcessPaymentAsync()
    {
        var method = _methodCombo.SelectedItem?.ToString() ?? MethodCash;

        // Validate credit: customer required
        if (method == MethodCredit)
        {
            if (_selectedCustomer == null)
            {
                _statusLabel.Text = "يرجى اختيار عميل للبيع الآجل";
                _statusLabel.Visible = true;
                return;
            }
        }

        // Validate cash: amount must be sufficient
        if (method == MethodCash)
        {
            if (_amountReceivedInput.Value < _totalDue)
            {
                _statusLabel.Text = "المبلغ المستلم أقل من المبلغ المستحق";
                _statusLabel.Visible = true;
                SetState(PaymentState.Insufficient);
                return;
            }
        }

        SetState(PaymentState.Processing);

        AmountReceived = method == MethodCash ? _amountReceivedInput.Value : _totalDue;
        ChangeAmount = AmountReceived - _totalDue;
        if (ChangeAmount < 0) ChangeAmount = 0;

        try
        {
            if (_saleService != null)
            {
                var reference = method == MethodCash ? null : _referenceTextBox.Text.Trim();
                var customerId = method == MethodCredit ? _selectedCustomer?.Id : (Guid?)null;
                var result = await _saleService.ProcessPaymentAsync(
                    new PaymentRequest(_saleId, _totalDue, method, reference, customerId));

                if (result.Success)
                {
                    SetState(PaymentState.Complete);
                    PaymentSucceeded?.Invoke(this, EventArgs.Empty);
                    await Task.Delay(1500);
                    Close();
                }
                else
                {
                    _statusLabel.Text = result.ErrorMessage ?? "فشلت عملية الدفع";
                    SetState(PaymentState.Error);
                }
            }
            else
            {
                await Task.Delay(1200);
                SetState(PaymentState.Complete);
                PaymentSucceeded?.Invoke(this, EventArgs.Empty);
                await Task.Delay(1500);
                Close();
            }
        }
        catch (Exception)
        {
            SetState(PaymentState.Error);
        }
    }

    public void ShowSuccess(decimal changeAmount)
    {
        _changeStatusLabel.Text = $"{changeAmount:N3} JOD";
        SetState(PaymentState.Complete);
    }

    public void ShowFailure(string errorMessage)
    {
        _statusLabel.Text = errorMessage;
        SetState(PaymentState.Error);
    }
}
