using System.Drawing;
using System.Windows.Forms;
using POS.Desktop.Themes;
using POS.Desktop.CustomControls;

namespace POS.Desktop.Forms;

/// <summary>
/// WD-001: Dialog for cash withdrawals and deposits during shift.
/// Inherits from RtlDialog. Fields: النوع (RadioButton: سحب / إيداع),
/// المبلغ (RtlNumericUpDown, 3 decimals, required),
/// السبب (RtlTextBox, required).
/// If withdrawal: validate amount &lt;= available cash.
/// Validation with Arabic messages. Confirm/Cancel.
/// </summary>
public class WithdrawalDepositDialog : RtlDialog
{
    private enum WdState
    {
        Ready,
        Validating,
        Processing,
        Error
    }

    private WdState _currentState = WdState.Ready;
    private decimal _availableCash;

    // UI Controls
    private Panel _typePanel = null!;
    private RadioButton _rbWithdrawal = null!;
    private RadioButton _rbDeposit = null!;
    private RtlNumericUpDown _numAmount = null!;
    private Label _lblPreviewAmount = null!;
    private RtlTextBox _txtReason = null!;
    private Label _lblAvailableCash = null!;
    private Label _lblValidation = null!;

    // Results
    public bool IsWithdrawal { get; private set; } = true;
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = "";

    // Events
    public event EventHandler<WithdrawalDepositEventArgs>? TransactionCompleted;

    /// <summary>
    /// Creates a new WithdrawalDepositDialog.
    /// </summary>
    /// <param name="availableCash">The current available cash in the shift drawer.</param>
    public WithdrawalDepositDialog(decimal availableCash) : base("سحب / إيداع نقدي", 460, 420)
    {
        _availableCash = availableCash;
        InitializeComponent();
        SetState(WdState.Ready);
    }

    private void InitializeComponent()
    {
        var layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 7,
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (int i = 0; i < 6; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Row 0: Type (RadioButton: سحب / إيداع)
        layout.Controls.Add(CreateLabel("النوع:"), 0, 0);

        _typePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Surface,
            Height = DesignTokens.ControlHeight.Standard
        };

        _rbWithdrawal = new RadioButton
        {
            Text = "💰 سحب",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.Error,
            RightToLeft = RightToLeft.Yes,
            Checked = true,
            AutoSize = true,
            Location = new Point(250, 10),
            Anchor = AnchorStyles.Right
        };
        _rbWithdrawal.CheckedChanged += (s, e) =>
        {
            if (_rbWithdrawal.Checked)
            {
                _lblAvailableCash.Visible = true;
                UpdateTypeUI();
            }
        };

        _rbDeposit = new RadioButton
        {
            Text = "💵 إيداع",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.Success,
            RightToLeft = RightToLeft.Yes,
            Checked = false,
            AutoSize = true,
            Location = new Point(130, 10),
            Anchor = AnchorStyles.Right
        };
        _rbDeposit.CheckedChanged += (s, e) =>
        {
            if (_rbDeposit.Checked)
            {
                _lblAvailableCash.Visible = false;
                UpdateTypeUI();
            }
        };

        _typePanel.Controls.Add(_rbWithdrawal);
        _typePanel.Controls.Add(_rbDeposit);
        layout.Controls.Add(_typePanel, 1, 0);

        // Row 1: Available Cash (shown only for withdrawals)
        layout.Controls.Add(new Label(), 0, 1);
        _lblAvailableCash = new Label
        {
            Text = $"الرصيد المتاح: {DesignTokens.FormatJOD(_availableCash)} JOD",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.Warning,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Micro)
        };
        layout.Controls.Add(_lblAvailableCash, 1, 1);

        // Row 2: Amount
        layout.Controls.Add(CreateLabel("المبلغ *:"), 0, 2);
        _numAmount = new RtlNumericUpDown
        {
            DecimalPlaces = 3,
            Minimum = 0,
            Maximum = 999999,
            Increment = 1.000m,
            Dock = DockStyle.Fill,
            Height = DesignTokens.ControlHeight.Standard
        };
        _numAmount.ValueChanged += (s, e) => UpdatePreview();
        layout.Controls.Add(_numAmount, 1, 2);

        // Row 3: Preview amount
        layout.Controls.Add(new Label(), 0, 3);
        _lblPreviewAmount = new Label
        {
            Text = "0.000 JOD",
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.Primary,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill
        };
        layout.Controls.Add(_lblPreviewAmount, 1, 3);

        // Row 4: Reason (required)
        layout.Controls.Add(CreateLabel("السبب *:"), 0, 4);
        _txtReason = new RtlTextBox
        {
            PlaceholderText = "أدخل سبب السحب أو الإيداع...",
            IsRequired = true,
            Dock = DockStyle.Fill,
            Height = DesignTokens.ControlHeight.Standard
        };
        layout.Controls.Add(_txtReason, 1, 4);

        // Row 5-6: Validation message
        layout.Controls.Add(new Label(), 0, 5);
        _lblValidation = new Label
        {
            Text = "",
            Font = DesignTokens.Typography.Secondary,
            ForeColor = DesignTokens.Colors.Error,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopRight,
            Visible = false,
            AutoSize = false,
            Height = 50
        };
        layout.SetRowSpan(_lblValidation, 2);
        layout.Controls.Add(_lblValidation, 1, 5);

        ContentArea.Controls.Add(layout);

        // Dialog actions
        AddAction("تأكيد", (s, e) => ConfirmTransaction(), true);
        AddAction("إلغاء", (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }, false);

        // Initialize UI
        UpdateTypeUI();

        // Load event
        Load += (s, e) => _numAmount.Focus();
    }

    // --- State Management ---

    private void SetState(WdState state)
    {
        _currentState = state;

        switch (state)
        {
            case WdState.Ready:
                _lblValidation.Visible = false;
                _txtReason.ClearError();
                _numAmount.Enabled = true;
                _rbWithdrawal.Enabled = true;
                _rbDeposit.Enabled = true;
                _txtReason.Enabled = true;
                break;

            case WdState.Validating:
                _numAmount.Enabled = true;
                _rbWithdrawal.Enabled = true;
                _rbDeposit.Enabled = true;
                _txtReason.Enabled = true;
                break;

            case WdState.Processing:
                _numAmount.Enabled = false;
                _rbWithdrawal.Enabled = false;
                _rbDeposit.Enabled = false;
                _txtReason.Enabled = false;
                break;

            case WdState.Error:
                _lblValidation.Visible = true;
                _numAmount.Enabled = true;
                _rbWithdrawal.Enabled = true;
                _rbDeposit.Enabled = true;
                _txtReason.Enabled = true;
                break;
        }
    }

    // --- UI Updates ---

    private void UpdateTypeUI()
    {
        var isWithdrawal = _rbWithdrawal.Checked;
        _lblPreviewAmount.ForeColor = isWithdrawal
            ? DesignTokens.Colors.Error
            : DesignTokens.Colors.Success;
        _lblAvailableCash.Visible = isWithdrawal;

        // Update dialog title
        DialogTitle = isWithdrawal ? "سحب نقدي" : "إيداع نقدي";
    }

    private void UpdatePreview()
    {
        var prefix = _rbWithdrawal.Checked ? "- " : "+ ";
        _lblPreviewAmount.Text = $"{prefix}{DesignTokens.FormatJOD(_numAmount.Value)} JOD";

        // Check if amount exceeds available cash for withdrawals
        if (_rbWithdrawal.Checked && _numAmount.Value > _availableCash)
        {
            _lblPreviewAmount.ForeColor = DesignTokens.Colors.Error;
        }
        else if (_rbWithdrawal.Checked && _numAmount.Value <= _availableCash)
        {
            _lblPreviewAmount.ForeColor = DesignTokens.Colors.Warning;
        }
        else
        {
            _lblPreviewAmount.ForeColor = DesignTokens.Colors.Success;
        }
    }

    // --- Validation ---

    private bool ValidateForm()
    {
        SetState(WdState.Validating);
        var errors = new List<string>();

        if (_numAmount.Value <= 0)
        {
            errors.Add("يجب إدخال مبلغ أكبر من صفر");
        }

        if (_rbWithdrawal.Checked && _numAmount.Value > _availableCash)
        {
            errors.Add($"المبلغ ({DesignTokens.FormatJOD(_numAmount.Value)} JOD) يتجاوز الرصيد المتاح ({DesignTokens.FormatJOD(_availableCash)} JOD)");
        }

        if (string.IsNullOrWhiteSpace(_txtReason.Text))
        {
            errors.Add("يجب إدخال سبب السحب أو الإيداع");
        }

        if (errors.Count > 0)
        {
            _lblValidation.Text = string.Join("\n", errors);
            SetState(WdState.Error);
            return false;
        }

        _lblValidation.Visible = false;
        SetState(WdState.Ready);
        return true;
    }

    // --- Confirm Transaction ---

    private void ConfirmTransaction()
    {
        if (!ValidateForm()) return;

        SetState(WdState.Processing);

        try
        {
            IsWithdrawal = _rbWithdrawal.Checked;
            Amount = _numAmount.Value;
            Reason = _txtReason.Text.Trim();

            // Raise event
            TransactionCompleted?.Invoke(this, new WithdrawalDepositEventArgs
            {
                IsWithdrawal = IsWithdrawal,
                Amount = Amount,
                Reason = Reason
            });

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[WithdrawalDepositDialog] Execute failed: {ex}");
            _lblValidation.Text = "حدث خطأ أثناء تنفيذ العملية";
            SetState(WdState.Error);
        }
    }

    // --- Helpers ---

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextPrimary,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, DesignTokens.Spacing.Micro, 0, 0)
        };
    }

    // --- EventArgs ---

    public class WithdrawalDepositEventArgs : EventArgs
    {
        public bool IsWithdrawal { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; } = "";
    }
}