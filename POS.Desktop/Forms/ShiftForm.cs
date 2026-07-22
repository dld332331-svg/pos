using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Application.Services;

using POS.Desktop.Themes;
namespace POS.Desktop.Forms;

/// <summary>
/// SHIFT-001: Shift management UserControl.
/// Open shift dialog: opening cash amount.
/// Current shift panel: shift info, sales summary, expense list, withdrawal/deposit buttons.
/// Close shift: shows expected/actual cash, variance, summary. All with Arabic labels.
/// </summary>
public class ShiftForm : UserControl
{
    private enum ShiftState
    {
        NoActiveShift,
        ActiveShift,
        Loading,
        Error,
        PermissionDenied
    }

    private readonly IShiftService? _shiftService;
    private ShiftState _currentState = ShiftState.NoActiveShift;
    private Guid _currentUserId;
    private ShiftDto? _currentShift;

    // UI Controls
    private Panel _headerPanel;
    private Button _openShiftButton;
    private Button _closeShiftButton;
    private Button _refreshButton;

    // Current shift panel
    private Panel _shiftInfoPanel;
    private Label _shiftNumberLabel;
    private Label _shiftUserLabel;
    private Label _shiftRegisterLabel;
    private Label _shiftOpenedAtLabel;
    private Label _shiftStatusValue;

    // Sales summary
    private Panel _summaryPanel;
    private Panel _totalSalesLabel;
    private Panel _totalCashLabel;
    private Panel _totalCardLabel;
    private Panel _totalReturnsLabel;
    private Panel _totalTransactionsLabel;

    // Cash operations
    private Panel _cashOpsPanel;
    private Button _withdrawalButton;
    private Button _depositButton;
    private DataGridView _expenseGrid;

    // Close shift panel
    private Panel _closeShiftPanel;
    private Label _expectedCashLabel;
    private Label _actualCashLabel;
    private NumericUpDown _actualCashInput;
    private Label _varianceLabel;
    private Label _varianceValue;

    // Overlay panels
    private Panel _noShiftPanel;
    private Panel _loadingPanel;
    private Panel _errorPanel;
    private Panel _permissionPanel;

    // Events
    public event EventHandler<decimal>? ShiftOpened;
    public event EventHandler? ShiftClosed;

    public ShiftForm()
    {
        InitializeComponent();
        SetState(ShiftState.NoActiveShift);
    }

    public ShiftForm(IShiftService shiftService, Guid userId) : this()
    {
        _shiftService = shiftService;
        _currentUserId = userId;
    }

    private void InitializeComponent()
    {
        RightToLeft = RightToLeft.Yes;
        BackColor = DesignTokens.BackgroundColor;
        Font = DesignTokens.DefaultFont;
        Dock = DockStyle.Fill;

        // Header
        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 45,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingSM)
        };

        var titleLbl = new Label { Text = "📋 إدارة الورديات", Font = DesignTokens.SubheadingFont, ForeColor = DesignTokens.TextPrimaryColor, Dock = DockStyle.Right, TextAlign = ContentAlignment.MiddleRight, Height = 40 };

        _openShiftButton = new Button { Text = "🟢 فتح وردية", Font = DesignTokens.ButtonFont, FlatStyle = FlatStyle.Flat, Size = new Size(120, 32), Dock = DockStyle.Left, BackColor = DesignTokens.SuccessColor, ForeColor = Color.White, Cursor = Cursors.Hand };
        _openShiftButton.Click += ShowOpenShiftDialog;

        _closeShiftButton = new Button { Text = "🔴 إغلاق وردية", Font = DesignTokens.ButtonFont, FlatStyle = FlatStyle.Flat, Size = new Size(120, 32), Dock = DockStyle.Left, BackColor = DesignTokens.ErrorColor, ForeColor = Color.White, Cursor = Cursors.Hand, Visible = false };
        _closeShiftButton.Click += ShowCloseShiftPanel;

        _refreshButton = new Button { Text = "🔄", Font = DesignTokens.DefaultFont, FlatStyle = FlatStyle.Flat, Size = new Size(32, 32), Dock = DockStyle.Left, BackColor = DesignTokens.CardColor, Cursor = Cursors.Hand, Margin = new Padding(0, 0, DesignTokens.SpacingSM, 0) };
        _refreshButton.Click += async (s, e) => await LoadCurrentShiftAsync();

        _headerPanel.Controls.Add(titleLbl);
        _headerPanel.Controls.Add(_openShiftButton);
        _headerPanel.Controls.Add(_closeShiftButton);
        _headerPanel.Controls.Add(_refreshButton);

        // Shift info panel
        _shiftInfoPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 120,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingMD),
            Margin = new Padding(DesignTokens.SpacingSM)
        };

        _shiftNumberLabel = CreateInfoRow("رقم الوردية:", "—", 5);
        _shiftUserLabel = CreateInfoRow("المستخدم:", "—", 30);
        _shiftRegisterLabel = CreateInfoRow("الجهاز:", "—", 55);
        _shiftOpenedAtLabel = CreateInfoRow("وقت الفتح:", "—", 80);

        _shiftStatusValue = new Label { Text = "نشطة", Font = new Font(DesignTokens.DefaultFont.FontFamily, 11f, FontStyle.Bold), ForeColor = DesignTokens.SuccessColor, Location = new Point(10, 10), Size = new Size(80, 25), BackColor = Color.FromArgb(232, 245, 233), TextAlign = ContentAlignment.MiddleCenter };

        _shiftInfoPanel.Controls.AddRange(new Control[] { _shiftStatusValue, _shiftNumberLabel, _shiftUserLabel, _shiftRegisterLabel, _shiftOpenedAtLabel });

        // Summary panel
        _summaryPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 150,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingMD),
            Margin = new Padding(DesignTokens.SpacingSM)
        };

        var summaryTitle = new Label { Text = "📊 ملخص المبيعات", Font = DesignTokens.SubheadingFont, ForeColor = DesignTokens.TextPrimaryColor, Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleRight };

        _totalSalesLabel = CreateSummaryValue("إجمالي المبيعات", "0.000 JOD", DesignTokens.PrimaryColor, 5);
        _totalCashLabel = CreateSummaryValue("مبيعات نقدية", "0.000 JOD", DesignTokens.SuccessColor, 40);
        _totalCardLabel = CreateSummaryValue("مبيعات بطاقة", "0.000 JOD", DesignTokens.InfoColor, 75);
        _totalReturnsLabel = CreateSummaryValue("المرتجعات", "0.000 JOD", DesignTokens.ErrorColor, 110);
        _totalTransactionsLabel = CreateSummaryValue("عدد العمليات", "٠", DesignTokens.TextPrimaryColor, 5, 180);

        _summaryPanel.Controls.AddRange(new Control[] { _totalTransactionsLabel, _totalSalesLabel, _totalCashLabel, _totalCardLabel, _totalReturnsLabel, summaryTitle });

        // Cash operations panel
        _cashOpsPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 200,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingMD),
            Margin = new Padding(DesignTokens.SpacingSM)
        };

        var cashTitle = new Label { Text = "💰 عمليات نقدية", Font = DesignTokens.SubheadingFont, ForeColor = DesignTokens.TextPrimaryColor, Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleRight };

        _withdrawalButton = new Button { Text = "💸 سحب", Font = DesignTokens.DefaultFont, FlatStyle = FlatStyle.Flat, Size = new Size(100, 30), Location = new Point(10, 35), BackColor = DesignTokens.WarningColor, ForeColor = Color.White, Cursor = Cursors.Hand };
        _depositButton = new Button { Text = "📥 إيداع", Font = DesignTokens.DefaultFont, FlatStyle = FlatStyle.Flat, Size = new Size(100, 30), Location = new Point(115, 35), BackColor = DesignTokens.InfoColor, ForeColor = Color.White, Cursor = Cursors.Hand };

        _expenseGrid = new DataGridView
        {
            Location = new Point(10, 75),
            Size = new Size(560, 110),
            ReadOnly = true,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            BackgroundColor = DesignTokens.SurfaceColor,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = DesignTokens.BorderColor,
            RightToLeft = RightToLeft.Yes,
            Font = DesignTokens.DataFont,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        _expenseGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "النوع", Name = "Type", FillWeight = 20 });
        _expenseGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "المبلغ", Name = "Amount", FillWeight = 20, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3" } });
        _expenseGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "السبب", Name = "Reason", FillWeight = 40 });
        _expenseGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الوقت", Name = "Time", FillWeight = 20 });

        _cashOpsPanel.Controls.AddRange(new Control[] { _expenseGrid, _withdrawalButton, _depositButton, cashTitle });

        // Close shift panel (initially hidden)
        _closeShiftPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingLG),
            Visible = false
        };

        var closeTitle = new Label { Text = "🔒 إغلاق الوردية", Font = DesignTokens.HeadingFont, ForeColor = DesignTokens.ErrorColor, Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter };

        _expectedCashLabel = CreateInfoRow("المبلغ المتوقع:", "0.000 JOD", 10);
        _actualCashLabel = new Label { Text = "المبلغ الفعلي:", Font = DesignTokens.DefaultFont, ForeColor = DesignTokens.TextPrimaryColor, Location = new Point(350, 50), Size = new Size(200, 25), TextAlign = ContentAlignment.MiddleRight };
        _actualCashInput = new NumericUpDown { Location = new Point(10, 48), Size = new Size(200, 28), Font = new Font(DesignTokens.DefaultFont.FontFamily, 12f), DecimalPlaces = 3, Minimum = 0, Maximum = 999999, ThousandsSeparator = true, RightToLeft = RightToLeft.Yes, TextAlign = HorizontalAlignment.Left };

        _varianceLabel = new Label { Text = "الفرق:", Font = DesignTokens.SubheadingFont, ForeColor = DesignTokens.TextSecondaryColor, Location = new Point(350, 85), Size = new Size(200, 30), TextAlign = ContentAlignment.MiddleRight };
        _varianceValue = new Label { Text = "0.000 JOD", Font = new Font(DesignTokens.DefaultFont.FontFamily, 16f, FontStyle.Bold), ForeColor = DesignTokens.SuccessColor, Location = new Point(10, 82), Size = new Size(200, 35), TextAlign = ContentAlignment.MiddleCenter };

        var confirmCloseBtn = new Button { Text = "تأكيد الإغلاق", Font = DesignTokens.ButtonFont, BackColor = DesignTokens.ErrorColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Size = new Size(180, 45), Dock = DockStyle.Bottom, Cursor = Cursors.Hand };
        confirmCloseBtn.Click += async (s, e) => await CloseShiftAsync();

        var cancelCloseBtn = new Button { Text = "إلغاء", Font = DesignTokens.ButtonFont, BackColor = DesignTokens.BorderColor, ForeColor = DesignTokens.TextPrimaryColor, FlatStyle = FlatStyle.Flat, Size = new Size(180, 45), Dock = DockStyle.Bottom, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 0, DesignTokens.SpacingSM) };
        cancelCloseBtn.Click += (s, e) => { _closeShiftPanel.Visible = false; _cashOpsPanel.Visible = true; };

        _actualCashInput.ValueChanged += (s, e) => UpdateVariance();

        _closeShiftPanel.Controls.AddRange(new Control[] { confirmCloseBtn, cancelCloseBtn, _actualCashLabel, _actualCashInput, _varianceLabel, _varianceValue, _expectedCashLabel, closeTitle });

        // No shift panel
        _noShiftPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.BackgroundColor };
        var noShiftIcon = new Label { Text = "🕐", Font = new Font("Segoe UI Emoji", 48), Dock = DockStyle.Top, Height = 80, TextAlign = ContentAlignment.MiddleCenter };
        var noShiftMsg = new Label { Text = "لا توجد وردية نشطة حالياً", Font = DesignTokens.SubheadingFont, ForeColor = DesignTokens.TextSecondaryColor, Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleCenter };
        var noShiftHint = new Label { Text = "اضغط \"فتح وردية\" لبدء وردية جديدة", Font = DesignTokens.DefaultFont, ForeColor = DesignTokens.TextHintColor, Dock = DockStyle.Top, Height = 25, TextAlign = ContentAlignment.MiddleCenter };
        _noShiftPanel.Controls.AddRange(new Control[] { noShiftHint, noShiftMsg, noShiftIcon });

        _loadingPanel = CreateOverlay("جاري تحميل بيانات الوردية...");
        _loadingPanel.Visible = false;
        _errorPanel = CreateOverlay("حدث خطأ أثناء تحميل الوردية");
        _errorPanel.Visible = false;
        _permissionPanel = CreateOverlay("ليس لديك صلاحية لإدارة الورديات");
        _permissionPanel.Visible = false;

        Controls.Add(_loadingPanel);
        Controls.Add(_errorPanel);
        Controls.Add(_permissionPanel);
        Controls.Add(_closeShiftPanel);
        Controls.Add(_noShiftPanel);
        Controls.Add(_cashOpsPanel);
        Controls.Add(_summaryPanel);
        Controls.Add(_shiftInfoPanel);
        Controls.Add(_headerPanel);
    }

    private Label CreateInfoRow(string labelText, string valueText, int y, int labelX = 350)
    {
        var label = new Label { Text = $"{labelText} {valueText}", Font = DesignTokens.DefaultFont, ForeColor = DesignTokens.TextPrimaryColor, Location = new Point(100, y), Size = new Size(labelX - 100, 22), TextAlign = ContentAlignment.MiddleRight };
        return label;
    }

    private Panel CreateSummaryValue(string title, string value, Color color, int x, int y = 35)
    {
        var panel = new Panel { Location = new Point(x, y), Size = new Size(175, 55), BackColor = DesignTokens.CardColor, Padding = new Padding(DesignTokens.SpacingSM) };
        var lbl = new Label { Text = title, Font = DesignTokens.SmallFont, ForeColor = DesignTokens.TextSecondaryColor, Dock = DockStyle.Top, Height = 18, TextAlign = ContentAlignment.MiddleCenter };
        var val = new Label { Text = value, Font = new Font(DesignTokens.DefaultFont.FontFamily, 12f, FontStyle.Bold), ForeColor = color, Dock = DockStyle.Bottom, Height = 28, TextAlign = ContentAlignment.MiddleCenter };
        panel.Controls.Add(val);
        panel.Controls.Add(lbl);
        return panel;
    }

    private Panel CreateOverlay(string text)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.BackgroundColor };
        panel.Controls.Add(new Label { Text = text, Font = DesignTokens.SubheadingFont, ForeColor = DesignTokens.TextSecondaryColor, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });
        return panel;
    }

    private void SetState(ShiftState state)
    {
        _currentState = state;
        bool hasShift = state == ShiftState.ActiveShift;

        _loadingPanel.Visible = state == ShiftState.Loading;
        _noShiftPanel.Visible = state == ShiftState.NoActiveShift;
        _errorPanel.Visible = state == ShiftState.Error;
        _permissionPanel.Visible = state == ShiftState.PermissionDenied;

        _shiftInfoPanel.Visible = hasShift;
        _summaryPanel.Visible = hasShift;
        _cashOpsPanel.Visible = hasShift && !_closeShiftPanel.Visible;

        _openShiftButton.Visible = !hasShift && state != ShiftState.Loading;
        _closeShiftButton.Visible = hasShift;
    }

    private void ShowOpenShiftDialog(object? sender, EventArgs e)
    {
        using var dialog = new Form
        {
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true,
            Text = "فتح وردية جديدة",
            ClientSize = new Size(350, 180),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            BackColor = DesignTokens.SurfaceColor,
            Font = DesignTokens.DefaultFont
        };

        var title = new Label { Text = "💵 المبلغ الافتتاحي", Font = DesignTokens.HeadingFont, ForeColor = DesignTokens.PrimaryColor, Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter };
        var label = new Label { Text = "أدخل المبلغ النقدي في الدرج:", Font = DesignTokens.DefaultFont, ForeColor = DesignTokens.TextPrimaryColor, Dock = DockStyle.Top, Height = 25, TextAlign = ContentAlignment.MiddleCenter };
        var input = new NumericUpDown { Dock = DockStyle.Top, Height = 35, Font = new Font(DesignTokens.DefaultFont.FontFamily, 14f), DecimalPlaces = 3, Minimum = 0, Maximum = 999999, Value = 0, ThousandsSeparator = true, RightToLeft = RightToLeft.Yes, TextAlign = HorizontalAlignment.Center };

        var panel = new Panel { Dock = DockStyle.Bottom, Height = 50 };
        var confirmBtn = new Button { Text = "فتح الوردية", Font = DesignTokens.ButtonFont, BackColor = DesignTokens.SuccessColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Size = new Size(150, 40), Dock = DockStyle.Right, Cursor = Cursors.Hand };
        var cancelBtn = new Button { Text = "إلغاء", Font = DesignTokens.ButtonFont, BackColor = DesignTokens.BorderColor, ForeColor = DesignTokens.TextPrimaryColor, FlatStyle = FlatStyle.Flat, Size = new Size(150, 40), Dock = DockStyle.Left, Cursor = Cursors.Hand };
        panel.Controls.Add(confirmBtn);
        panel.Controls.Add(cancelBtn);

        dialog.Controls.Add(panel);
        dialog.Controls.Add(input);
        dialog.Controls.Add(label);
        dialog.Controls.Add(title);

        confirmBtn.Click += async (s, e) =>
        {
            confirmBtn.Enabled = false;
            try
            {
                if (_shiftService != null)
                    _currentShift = await _shiftService.OpenShiftAsync(new OpenShiftRequest(input.Value, Guid.Empty), _currentUserId);
                else
                {
                    await Task.Delay(500);
                    _currentShift = new ShiftDto(Guid.NewGuid(), 1, "المستخدم", "الجهاز الرئيسي", input.Value, null, 0, 0, 0, null, null, null, "Open", DateTime.Now, null);
                }
                ShiftOpened?.Invoke(this, input.Value);
                UpdateShiftDisplay();
                SetState(ShiftState.ActiveShift);
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            }
            catch (Exception ex)
            {
                RtlMessageBox.Show(ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                confirmBtn.Enabled = true;
            }
        };

        cancelBtn.Click += (s, e) => dialog.Close();
        dialog.ShowDialog();
    }

    private void ShowCloseShiftPanel(object? sender, EventArgs e)
    {
        if (_currentShift == null) return;
        _expectedCashLabel.Text = $"المبلغ المتوقع: {_currentShift.OpeningCash + _currentShift.TotalCashSales:N3} JOD";
        _actualCashInput.Value = _currentShift.OpeningCash + _currentShift.TotalCashSales;
        _cashOpsPanel.Visible = false;
        _closeShiftPanel.Visible = true;
    }

    private void UpdateVariance()
    {
        if (_currentShift == null) return;
        var expected = _currentShift.OpeningCash + _currentShift.TotalCashSales;
        var variance = _actualCashInput.Value - expected;

        _varianceValue.Text = $"{variance:+0.000;-0.000;0.000} JOD";
        _varianceValue.ForeColor = variance == 0 ? DesignTokens.SuccessColor : variance > 0 ? DesignTokens.InfoColor : DesignTokens.ErrorColor;
    }

    private async Task CloseShiftAsync()
    {
        if (_currentShift == null) return;

        try
        {
            if (_shiftService != null)
            {
                var result = await _shiftService.CloseShiftAsync(new CloseShiftRequest(_currentShift.Id, _actualCashInput.Value), _currentUserId);
                _currentShift = result;
            }

            ShiftClosed?.Invoke(this, EventArgs.Empty);
            _closeShiftPanel.Visible = false;
            SetState(ShiftState.NoActiveShift);
            RtlMessageBox.Show("تم إغلاق الوردية بنجاح", "نجاح", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }
        catch (Exception ex)
        {
            RtlMessageBox.Show($"خطأ في إغلاق الوردية: {ex.Message}", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateShiftDisplay()
    {
        if (_currentShift == null) return;
        _shiftNumberLabel.Text = $"رقم الوردية: #{_currentShift.ShiftNumber}";
        _shiftUserLabel.Text = $"المستخدم: {_currentShift.UserName}";
        _shiftRegisterLabel.Text = $"الجهاز: {_currentShift.RegisterName}";
        _shiftOpenedAtLabel.Text = $"وقت الفتح: {_currentShift.OpenedAt:yyyy/MM/dd HH:mm}";
        ((Label)_totalSalesLabel.Controls[0]).Text = $"إجمالي المبيعات: {_currentShift.TotalSales:N3} JOD";
    }

    public async Task LoadCurrentShiftAsync()
    {
        SetState(ShiftState.Loading);
        try
        {
            if (_shiftService != null)
                _currentShift = await _shiftService.GetCurrentShiftAsync(_currentUserId);
            else
                await Task.Delay(300);

            if (_currentShift != null && _currentShift.Status == "Open")
            {
                UpdateShiftDisplay();
                SetState(ShiftState.ActiveShift);
            }
            else
            {
                SetState(ShiftState.NoActiveShift);
            }
        }
        catch (UnauthorizedAccessException) { SetState(ShiftState.PermissionDenied); }
        catch { SetState(ShiftState.Error); }
    }
}