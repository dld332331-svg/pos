using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Application.Services;

using POS.Desktop.Themes;
namespace POS.Desktop.Forms;

/// <summary>
/// INV-002: Stock adjustment dialog.
/// Fields: Product (readonly), Current Quantity (readonly), New Quantity (numeric), Reason (textbox, required).
/// Validate: new qty >= 0. Confirm/Cancel. RTL layout.
/// </summary>
public class StockAdjustmentDialog : Form
{
    private readonly IInventoryService? _inventoryService;
    private readonly InventoryStatusDto _product;
    private readonly bool _isWasteMode;
    private readonly Guid _currentUserId;

    private enum StockAdjustmentState { Loading, Loaded, Error, PermissionDenied }
    private StockAdjustmentState _currentState = StockAdjustmentState.Loading;

    // UI Controls
    private Panel _mainPanel;
    private Label _titleLabel;
    private Label _productLabel;
    private Label _productNameValue;
    private Label _currentQtyLabel;
    private Label _currentQtyValue;
    private Label _newQtyLabel;
    private NumericUpDown _newQtyInput;
    private Label _reasonLabel;
    private TextBox _reasonTextBox;
    private Label _differenceLabel;
    private Label _differenceValue;
    private ErrorProvider _errorProvider;
    private Button _confirmButton;
    private Button _cancelButton;

    // Overlay panels
    private Panel _loadingPanel = null!;
    private Panel _errorPanel = null!;
    private Panel _permissionPanel = null!;
    private Label _errorLabel = null!;

    // Events
    public event EventHandler? AdjustmentCompleted;

    public StockAdjustmentDialog(InventoryStatusDto product, Guid userId, bool isWasteMode = false)
    {
        _product = product;
        _isWasteMode = isWasteMode;
        _currentUserId = userId;
        InitializeComponent();
        PopulateData();
        SetState(StockAdjustmentState.Loaded);
    }

    public StockAdjustmentDialog(InventoryStatusDto product, Guid userId, IInventoryService inventoryService, bool isWasteMode = false) : this(product, userId, isWasteMode)
    {
        _inventoryService = inventoryService;
    }

    private void InitializeComponent()
    {
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Text = _isWasteMode ? "تسجيل هالك" : "تعديل المخزون";
        ClientSize = new Size(420, 380);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = DesignTokens.BackgroundColor;
        Font = DesignTokens.DefaultFont;

        _errorProvider = new ErrorProvider { RightToLeft = true };

        _mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingLG)
        };

        // Title
        _titleLabel = new Label
        {
            Text = _isWasteMode ? "🗑️ تسجيل هالك" : "📏 تعديل كمية المخزون",
            Font = DesignTokens.HeadingFont,
            ForeColor = _isWasteMode ? DesignTokens.WarningColor : DesignTokens.PrimaryColor,
            Dock = DockStyle.Top,
            Height = 45,
            TextAlign = ContentAlignment.MiddleCenter
        };

        // Product info
        _productLabel = new Label
        {
            Text = "المنتج:",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            Location = new Point(230, 15),
            Size = new Size(160, 22),
            TextAlign = ContentAlignment.MiddleRight
        };

        _productNameValue = new Label
        {
            Text = "",
            Font = new Font(DesignTokens.DefaultFont.FontFamily, 10f, FontStyle.Bold),
            ForeColor = DesignTokens.TextPrimaryColor,
            Location = new Point(10, 15),
            Size = new Size(215, 22),
            TextAlign = ContentAlignment.MiddleRight
        };

        // Current quantity
        _currentQtyLabel = new Label
        {
            Text = "الكمية الحالية:",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            Location = new Point(230, 50),
            Size = new Size(160, 22),
            TextAlign = ContentAlignment.MiddleRight
        };

        _currentQtyValue = new Label
        {
            Text = "",
            Font = new Font(DesignTokens.DefaultFont.FontFamily, 12f, FontStyle.Bold),
            ForeColor = DesignTokens.PrimaryColor,
            Location = new Point(10, 47),
            Size = new Size(215, 28),
            TextAlign = ContentAlignment.MiddleCenter
        };

        // New quantity
        _newQtyLabel = new Label
        {
            Text = _isWasteMode ? "الكمية الهالكة:" : "الكمية الجديدة:",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextPrimaryColor,
            Location = new Point(230, 95),
            Size = new Size(160, 22),
            TextAlign = ContentAlignment.MiddleRight
        };

        _newQtyInput = new NumericUpDown
        {
            Location = new Point(10, 93),
            Size = new Size(215, 28),
            Font = new Font(DesignTokens.DefaultFont.FontFamily, 12f),
            DecimalPlaces = 0,
            Minimum = 0,
            Maximum = 999999,
            Value = 0,
            RightToLeft = RightToLeft.Yes,
            TextAlign = HorizontalAlignment.Center
        };
        _newQtyInput.ValueChanged += (s, e) => UpdateDifference();

        // Difference
        _differenceLabel = new Label
        {
            Text = "الفرق:",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            Location = new Point(230, 130),
            Size = new Size(160, 22),
            TextAlign = ContentAlignment.MiddleRight
        };

        _differenceValue = new Label
        {
            Text = "٠",
            Font = new Font(DesignTokens.DefaultFont.FontFamily, 12f, FontStyle.Bold),
            ForeColor = DesignTokens.TextSecondaryColor,
            Location = new Point(10, 128),
            Size = new Size(215, 28),
            TextAlign = ContentAlignment.MiddleCenter
        };

        // Reason
        _reasonLabel = new Label
        {
            Text = "السبب *:",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextPrimaryColor,
            Location = new Point(230, 175),
            Size = new Size(160, 22),
            TextAlign = ContentAlignment.MiddleRight
        };

        _reasonTextBox = new TextBox
        {
            Location = new Point(10, 173),
            Size = new Size(380, 80),
            Font = DesignTokens.DefaultFont,
            RightToLeft = RightToLeft.Yes,
            Multiline = true,
            PlaceholderText = "أدخل سبب التعديل...",
            ScrollBars = ScrollBars.Vertical
        };

        // Actions
        var actionsPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingMD)
        };

        _cancelButton = new Button
        {
            Text = "إلغاء",
            Font = DesignTokens.ButtonFont,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(170, 40),
            Dock = DockStyle.Left,
            Cursor = Cursors.Hand,
            BackColor = DesignTokens.BorderColor,
            ForeColor = DesignTokens.TextPrimaryColor
        };

        _confirmButton = new Button
        {
            Text = _isWasteMode ? "تسجيل الهالك" : "تأكيد التعديل",
            Font = DesignTokens.ButtonFont,
            ForeColor = Color.White,
            BackColor = _isWasteMode ? DesignTokens.WarningColor : DesignTokens.SuccessColor,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(170, 40),
            Dock = DockStyle.Right,
            Cursor = Cursors.Hand
        };

        actionsPanel.Controls.Add(_confirmButton);
        actionsPanel.Controls.Add(_cancelButton);

        // Assemble
        _mainPanel.Controls.Add(_productLabel);
        _mainPanel.Controls.Add(_productNameValue);
        _mainPanel.Controls.Add(_currentQtyLabel);
        _mainPanel.Controls.Add(_currentQtyValue);
        _mainPanel.Controls.Add(_newQtyLabel);
        _mainPanel.Controls.Add(_newQtyInput);
        _mainPanel.Controls.Add(_differenceLabel);
        _mainPanel.Controls.Add(_differenceValue);
        _mainPanel.Controls.Add(_reasonLabel);
        _mainPanel.Controls.Add(_reasonTextBox);

        Controls.Add(_mainPanel);
        Controls.Add(actionsPanel);
        Controls.Add(_titleLabel);

        // Overlay panels
        _loadingPanel = ThemeManager.CreateLoadingPanel("جاري تحميل بيانات التسوية...");
        _loadingPanel.Visible = false;

        _errorPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.BackgroundColor, Visible = false };
        _errorLabel = new Label
        {
            Text = "حدث خطأ أثناء تحميل البيانات",
            Font = DesignTokens.SubheadingFont,
            ForeColor = DesignTokens.ErrorColor,
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var retryButton = new Button
        {
            Text = "إعادة المحاولة",
            Font = DesignTokens.ButtonFont,
            BackColor = DesignTokens.PrimaryColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(150, 40),
            Cursor = Cursors.Hand
        };
        retryButton.Anchor = AnchorStyles.None;
        retryButton.Click += async (s, e) => { SetState(StockAdjustmentState.Loading); PopulateData(); SetState(StockAdjustmentState.Loaded); };
        _errorPanel.Controls.Add(retryButton);
        _errorPanel.Controls.Add(_errorLabel);

        _permissionPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.BackgroundColor, Visible = false };
        _permissionPanel.Controls.Add(new Label
        {
            Text = "ليس لديك صلاحية لتسوية المخزون",
            Font = DesignTokens.SubheadingFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        });

        Controls.Add(_loadingPanel);
        Controls.Add(_errorPanel);
        Controls.Add(_permissionPanel);

        // Events
        _confirmButton.Click += async (s, e) => await ConfirmAsync();
        _cancelButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };
    }

    private void PopulateData()
    {
        _productNameValue.Text = _product.ProductName;
        _currentQtyValue.Text = $"{_product.Quantity} {_product.Unit}";
        _newQtyInput.Maximum = _isWasteMode ? _product.AvailableQuantity : 999999;
        UpdateDifference();
    }

    private void UpdateDifference()
    {
        if (_isWasteMode)
        {
            var diff = _newQtyInput.Value;
            _differenceValue.Text = $"- {diff:N0} {_product.Unit}";
            _differenceValue.ForeColor = diff > 0 ? DesignTokens.ErrorColor : DesignTokens.TextSecondaryColor;
        }
        else
        {
            var diff = _newQtyInput.Value - _product.Quantity;
            var sign = diff >= 0 ? "+" : "";
            _differenceValue.Text = $"{sign}{diff:N0} {_product.Unit}";
            _differenceValue.ForeColor = diff == 0 ? DesignTokens.TextSecondaryColor : diff > 0 ? DesignTokens.SuccessColor : DesignTokens.ErrorColor;
        }
    }

    private bool ValidateForm()
    {
        _errorProvider.Clear();

        if (_isWasteMode && _newQtyInput.Value <= 0)
        {
            _errorProvider.SetError(_newQtyInput, "يجب إدخال كمية أكبر من صفر");
            return false;
        }

        if (!_isWasteMode && _newQtyInput.Value < 0)
        {
            _errorProvider.SetError(_newQtyInput, "الكمية لا يمكن أن تكون سالبة");
            return false;
        }

        if (string.IsNullOrWhiteSpace(_reasonTextBox.Text))
        {
            _errorProvider.SetError(_reasonTextBox, "السبب مطلوب");
            return false;
        }

        return true;
    }

    private void SetState(StockAdjustmentState state)
    {
        _currentState = state;
        _loadingPanel.Visible = state == StockAdjustmentState.Loading;
        _errorPanel.Visible = state == StockAdjustmentState.Error;
        _permissionPanel.Visible = state == StockAdjustmentState.PermissionDenied;
        _mainPanel.Visible = state == StockAdjustmentState.Loaded;
    }

    private async Task ConfirmAsync()
    {
        if (!ValidateForm()) return;

        SetState(StockAdjustmentState.Loading);
        _confirmButton.Enabled = false;
        _confirmButton.Text = "جاري الحفظ...";

        try
        {
            if (_inventoryService != null)
            {
                OperationResult result;

                if (_isWasteMode)
                {
                    result = await _inventoryService.RecordWasteAsync(
                        new WasteRecordRequest(_product.ProductId, _newQtyInput.Value, _reasonTextBox.Text.Trim()),
                        _currentUserId);
                }
                else
                {
                    result = await _inventoryService.AdjustStockAsync(
                        new StockAdjustmentRequest(_product.ProductId, _newQtyInput.Value, _reasonTextBox.Text.Trim()),
                        _currentUserId);
                }

                if (!result.Success)
                {
                    RtlMessageBox.Show(result.ErrorMessage ?? "فشل العملية", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error,
                        MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                    return;
                }
            }

            AdjustmentCompleted?.Invoke(this, EventArgs.Empty);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[StockAdjustmentDialog] Save failed: {ex}");
            _errorLabel.Text = "حدث خطأ أثناء تنفيذ العملية";
            SetState(StockAdjustmentState.Error);
        }
        finally
        {
            _confirmButton.Enabled = true;
            _confirmButton.Text = _isWasteMode ? "تسجيل الهالك" : "تأكيد التعديل";
        }
    }
}