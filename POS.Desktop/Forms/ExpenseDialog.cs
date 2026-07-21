using System.Drawing;
using System.Windows.Forms;
using POS.Desktop.Themes;
using POS.Desktop.CustomControls;

namespace POS.Desktop.Forms;

/// <summary>
/// EXP-001: Dialog for recording expenses during a shift.
/// Inherits from RtlDialog. Fields: المبلغ (RtlNumericUpDown, 3 decimals, required),
/// الفئة (ComboBox: مصاريف تشغيل, صيانة, نقل, أخرى),
/// السبب/الوصف (RtlTextBox, required), التاريخ (DateTimePicker, defaults to now).
/// Validation: amount &gt; 0, reason required. Confirm/Cancel. Arabic messages.
/// </summary>
public class ExpenseDialog : RtlDialog
{
    private enum ExpenseState
    {
        Ready,
        Validating,
        Saving,
        Error
    }

    private ExpenseState _currentState = ExpenseState.Ready;

    // Category display mapping
    private static readonly Dictionary<string, string> CategoryIcons = new()
    {
        { "مصاريف تشغيل", "🔧" },
        { "صيانة", "🛠" },
        { "نقل", "🚚" },
        { "أخرى", "📋" }
    };

    private static readonly Dictionary<string, Color> CategoryColors = new()
    {
        { "مصاريف تشغيل", DesignTokens.Colors.Info },
        { "صيانة", DesignTokens.Colors.Warning },
        { "نقل", DesignTokens.Colors.Primary },
        { "أخرى", DesignTokens.Colors.TextSecondary }
    };

    // UI Controls
    private RtlNumericUpDown _numAmount = null!;
    private RtlComboBox _cboCategory = null!;
    private RtlTextBox _txtReason = null!;
    private DateTimePicker _dtpDate = null!;
    private Label _lblValidation = null!;
    private Label _lblPreviewAmount = null!;

    // Results
    public decimal RecordedAmount { get; private set; }
    public string RecordedCategory { get; private set; } = "";
    public string RecordedReason { get; private set; } = "";
    public DateTime RecordedDate { get; private set; }

    // Events
    public event EventHandler<ExpenseRecordedEventArgs>? ExpenseRecorded;

    public ExpenseDialog() : base("تسجيل مصروف", 450, 400)
    {
        InitializeComponent();
        SetState(ExpenseState.Ready);
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

        // Row 0: Category
        layout.Controls.Add(CreateLabel("الفئة:"), 0, 0);
        _cboCategory = new RtlComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Height = DesignTokens.ControlHeight.Standard
        };
        _cboCategory.Items.AddRange(new object[] { "مصاريف تشغيل", "صيانة", "نقل", "أخرى" });
        _cboCategory.SelectedIndex = 0;
        _cboCategory.SelectedIndexChanged += (s, e) => UpdateCategoryDisplay();
        layout.Controls.Add(_cboCategory, 1, 0);

        // Row 1: Amount
        layout.Controls.Add(CreateLabel("المبلغ *:"), 0, 1);
        _numAmount = new RtlNumericUpDown
        {
            DecimalPlaces = 3,
            Minimum = 0,
            Maximum = 999999,
            Increment = 0.500m,
            Dock = DockStyle.Fill,
            Height = DesignTokens.ControlHeight.Standard
        };
        _numAmount.ValueChanged += (s, e) => UpdatePreview();
        layout.Controls.Add(_numAmount, 1, 1);

        // Row 2: Preview amount
        layout.Controls.Add(new Label(), 0, 2);
        _lblPreviewAmount = new Label
        {
            Text = "0.000 JOD",
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.Primary,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill
        };
        layout.Controls.Add(_lblPreviewAmount, 1, 2);

        // Row 3: Reason/Description (required)
        layout.Controls.Add(CreateLabel("السبب/الوصف *:"), 0, 3);
        _txtReason = new RtlTextBox
        {
            PlaceholderText = "أدخل سبب أو وصف المصروف...",
            IsRequired = true,
            Dock = DockStyle.Fill,
            Height = DesignTokens.ControlHeight.Standard
        };
        layout.Controls.Add(_txtReason, 1, 3);

        // Row 4-5: Date
        layout.Controls.Add(CreateLabel("التاريخ:"), 0, 4);
        _dtpDate = new DateTimePicker
        {
            Value = DateTime.Now,
            Format = DateTimePickerFormat.Short,
            RightToLeft = RightToLeft.Yes,
            Font = DesignTokens.Typography.Input,
            Dock = DockStyle.Fill,
            Height = DesignTokens.ControlHeight.Standard
        };
        layout.Controls.Add(_dtpDate, 1, 4);

        // Row 6: Validation message
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
            Height = 40
        };
        layout.Controls.Add(_lblValidation, 1, 5);

        ContentArea.Controls.Add(layout);

        // Dialog actions
        AddAction("تسجيل المصروف", (s, e) => SaveExpense(), true);
        AddAction("إلغاء", (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }, false);

        // Load event
        Load += (s, e) => _numAmount.Focus();
    }

    // --- State Management ---

    private void SetState(ExpenseState state)
    {
        _currentState = state;

        switch (state)
        {
            case ExpenseState.Ready:
                _lblValidation.Visible = false;
                _txtReason.ClearError();
                _numAmount.Enabled = true;
                _cboCategory.Enabled = true;
                _txtReason.Enabled = true;
                _dtpDate.Enabled = true;
                break;

            case ExpenseState.Validating:
                _numAmount.Enabled = true;
                _cboCategory.Enabled = true;
                _txtReason.Enabled = true;
                _dtpDate.Enabled = true;
                break;

            case ExpenseState.Saving:
                // Buttons are managed by RtlButton.IsLoading
                _numAmount.Enabled = false;
                _cboCategory.Enabled = false;
                _txtReason.Enabled = false;
                _dtpDate.Enabled = false;
                break;

            case ExpenseState.Error:
                _lblValidation.Visible = true;
                _numAmount.Enabled = true;
                _cboCategory.Enabled = true;
                _txtReason.Enabled = true;
                _dtpDate.Enabled = true;
                break;
        }
    }

    // --- Validation ---

    private bool ValidateForm()
    {
        SetState(ExpenseState.Validating);
        var errors = new List<string>();

        if (_numAmount.Value <= 0)
        {
            errors.Add("يجب إدخال مبلغ أكبر من صفر");
        }

        if (string.IsNullOrWhiteSpace(_txtReason.Text))
        {
            errors.Add("يجب إدخال سبب أو وصف المصروف");
        }

        if (errors.Count > 0)
        {
            _lblValidation.Text = string.Join("\n", errors);
            SetState(ExpenseState.Error);
            return false;
        }

        _lblValidation.Visible = false;
        SetState(ExpenseState.Ready);
        return true;
    }

    // --- Save ---

    private void SaveExpense()
    {
        if (!ValidateForm()) return;

        SetState(ExpenseState.Saving);

        try
        {
            // Simulate async save
            RecordedAmount = _numAmount.Value;
            RecordedCategory = _cboCategory.Text;
            RecordedReason = _txtReason.Text.Trim();
            RecordedDate = _dtpDate.Value;

            // Raise event
            ExpenseRecorded?.Invoke(this, new ExpenseRecordedEventArgs
            {
                Amount = RecordedAmount,
                Category = RecordedCategory,
                Reason = RecordedReason,
                Date = RecordedDate
            });

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _lblValidation.Text = $"حدث خطأ أثناء حفظ المصروف: {ex.Message}";
            SetState(ExpenseState.Error);
        }
    }

    // --- Helpers ---

    private void UpdatePreview()
    {
        var amount = _numAmount.Value;
        _lblPreviewAmount.Text = $"{DesignTokens.FormatJOD(amount)} JOD";

        // Color coding based on amount
        if (amount > 100)
        {
            _lblPreviewAmount.ForeColor = DesignTokens.Colors.Error;
        }
        else if (amount > 50)
        {
            _lblPreviewAmount.ForeColor = DesignTokens.Colors.Warning;
        }
        else
        {
            _lblPreviewAmount.ForeColor = DesignTokens.Colors.Primary;
        }
    }

    private void UpdateCategoryDisplay()
    {
        var category = _cboCategory.Text;
        if (CategoryColors.TryGetValue(category, out var color))
        {
            _lblPreviewAmount.ForeColor = color;
        }
    }

    /// <summary>
    /// Gets the icon for a given expense category.
    /// </summary>
    public static string GetCategoryIcon(string category)
    {
        return CategoryIcons.TryGetValue(category, out var icon) ? icon : "📋";
    }

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

    public class ExpenseRecordedEventArgs : EventArgs
    {
        public decimal Amount { get; set; }
        public string Category { get; set; } = "";
        public string Reason { get; set; } = "";
        public DateTime Date { get; set; }
    }
}