using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Application.Services;

using POS.Desktop.Themes;
namespace POS.Desktop.Forms;

/// <summary>
/// INV-001: Inventory management UserControl.
/// Top: search + filter by stock status + adjust/waste buttons.
/// Middle: DataGridView (Product, Current Stock, Reserved, Available, Min Stock, Status, Last Movement).
/// Movement history tab with DataGridView. All Arabic.
/// </summary>
public class InventoryForm : UserControl
{
    private enum InventoryState
    {
        Loading,
        Loaded,
        Empty,
        Error,
        PermissionDenied
    }

    private readonly IInventoryService? _inventoryService;
    private InventoryState _currentState = InventoryState.Loading;

    // UI Controls
    private Panel _toolbarPanel;
    private TextBox _searchTextBox;
    private ComboBox _stockFilterCombo;
    private Button _searchButton;
    private Button _refreshButton;
    private Button _adjustButton;
    private Button _wasteButton;

    private TabControl _mainTabControl;
    private TabPage _stockTabPage;
    private TabPage _movementTabPage;

    private DataGridView _stockGrid;
    private DataGridView _movementGrid;
    private Panel _loadingPanel;
    private Panel _emptyPanel;
    private Panel _errorPanel;
    private Panel _permissionPanel;
    private Label _errorLabel;
    private Button _retryButton;
    private Panel _summaryPanel;
    private Label _totalItemsLabel;
    private Label _lowStockLabel;
    private Label _outOfStockLabel;

    // Events
    public event EventHandler<InventoryStatusDto>? RequestAdjustment;
    public event EventHandler<InventoryStatusDto>? RequestWaste;

    public InventoryForm()
    {
        InitializeComponent();
        SetState(InventoryState.Loading);
    }

    public InventoryForm(IInventoryService inventoryService) : this()
    {
        _inventoryService = inventoryService;
    }

    private void InitializeComponent()
    {
        RightToLeft = RightToLeft.Yes;
        BackColor = DesignTokens.BackgroundColor;
        Font = DesignTokens.DefaultFont;
        Dock = DockStyle.Fill;

        // Toolbar
        _toolbarPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingSM),
            Margin = new Padding(0, 0, 0, DesignTokens.SpacingSM)
        };

        _searchTextBox = new TextBox
        {
            Location = new Point(250, 10),
            Size = new Size(220, 28),
            Font = DesignTokens.DefaultFont,
            RightToLeft = RightToLeft.Yes,
            PlaceholderText = "🔍 بحث باسم المنتج..."
        };
        _searchTextBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; _ = LoadStockAsync(); } };

        _stockFilterCombo = new ComboBox
        {
            Location = new Point(160, 10),
            Width = 85,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = DesignTokens.DefaultFont,
            RightToLeft = RightToLeft.Yes
        };
        _stockFilterCombo.Items.AddRange(new object[] { "جميع الحالات", "متوفر", "منخفض", "نفذ" });
        _stockFilterCombo.SelectedIndex = 0;

        _searchButton = new Button { Text = "بحث", Font = DesignTokens.DefaultFont, FlatStyle = FlatStyle.Flat, Location = new Point(475, 10), Size = new Size(60, 28), BackColor = DesignTokens.PrimaryColor, ForeColor = Color.White, Cursor = Cursors.Hand };
        _searchButton.Click += async (s, e) => await LoadStockAsync();

        _refreshButton = new Button { Text = "🔄", Font = DesignTokens.DefaultFont, FlatStyle = FlatStyle.Flat, Location = new Point(540, 10), Size = new Size(28, 28), BackColor = DesignTokens.CardColor, Cursor = Cursors.Hand };
        _refreshButton.Click += async (s, e) => await LoadStockAsync();

        _adjustButton = new Button { Text = "📏 تعديل مخزون", Font = DesignTokens.DefaultFont, FlatStyle = FlatStyle.Flat, Location = new Point(10, 8), Size = new Size(110, 32), BackColor = DesignTokens.InfoColor, ForeColor = Color.White, Cursor = Cursors.Hand };
        _adjustButton.Click += AdjustButton_Click;

        _wasteButton = new Button { Text = "🗑️ تسجيل هالك", Font = DesignTokens.DefaultFont, FlatStyle = FlatStyle.Flat, Location = new Point(125, 8), Size = new Size(110, 32), BackColor = DesignTokens.WarningColor, ForeColor = Color.White, Cursor = Cursors.Hand };
        _wasteButton.Click += WasteButton_Click;

        _toolbarPanel.Controls.AddRange(new Control[] { _searchButton, _refreshButton, _searchTextBox, _stockFilterCombo, _adjustButton, _wasteButton });

        // Summary panel
        _summaryPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingSM),
            Margin = new Padding(0, 0, 0, DesignTokens.SpacingSM),
            FlowDirection = FlowDirection.RightToLeft
        };

        _totalItemsLabel = new Label { Text = "📦 إجمالي الأصناف: ٠", Font = DesignTokens.DefaultFont, ForeColor = DesignTokens.TextPrimaryColor, AutoSize = true, Margin = new Padding(0, 0, DesignTokens.SpacingLG, 0) };
        _lowStockLabel = new Label { Text = "⚠️ مخزون منخفض: ٠", Font = DesignTokens.DefaultFont, ForeColor = DesignTokens.WarningColor, AutoSize = true, Margin = new Padding(0, 0, DesignTokens.SpacingLG, 0) };
        _outOfStockLabel = new Label { Text = "❌ نفذ من المخزون: ٠", Font = DesignTokens.DefaultFont, ForeColor = DesignTokens.ErrorColor, AutoSize = true };

        _summaryPanel.Controls.AddRange(new Control[] { _totalItemsLabel, _lowStockLabel, _outOfStockLabel });

        // Tab control
        _mainTabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes
        };

        // Stock tab
        _stockTabPage = new TabPage { Text = "📊 المخزون الحالي", Padding = new Padding(DesignTokens.SpacingSM) };

        _stockGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            BackgroundColor = DesignTokens.SurfaceColor,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = DesignTokens.BorderColor,
            RightToLeft = RightToLeft.Yes,
            Font = DesignTokens.DataFont,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        _stockGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "المنتج", Name = "Product", FillWeight = 22 });
        _stockGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "المخزون الحالي", Name = "CurrentStock", FillWeight = 12, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _stockGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "محجوز", Name = "Reserved", FillWeight = 10, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _stockGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "متاح", Name = "Available", FillWeight = 12, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _stockGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الحد الأدنى", Name = "MinStock", FillWeight = 10, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _stockGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الحالة", Name = "Status", FillWeight = 12 });
        _stockGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "آخر حركة", Name = "LastMovement", FillWeight = 22 });
        _stockGrid.CellFormatting += StockGrid_CellFormatting;
        _stockGrid.CellDoubleClick += StockGrid_CellDoubleClick;

        _stockTabPage.Controls.Add(_stockGrid);

        // Movement history tab
        _movementTabPage = new TabPage { Text = "📋 سجل الحركات", Padding = new Padding(DesignTokens.SpacingSM) };

        _movementGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            BackgroundColor = DesignTokens.SurfaceColor,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = DesignTokens.BorderColor,
            RightToLeft = RightToLeft.Yes,
            Font = DesignTokens.DataFont,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        _movementGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "التاريخ", Name = "Date", FillWeight = 18 });
        _movementGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "المنتج", Name = "Product", FillWeight = 18 });
        _movementGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "نوع الحركة", Name = "Type", FillWeight = 12 });
        _movementGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الكمية", Name = "Quantity", FillWeight = 8, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _movementGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "قبل", Name = "Before", FillWeight = 8, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _movementGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "بعد", Name = "After", FillWeight = 8, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _movementGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "السبب", Name = "Reason", FillWeight = 18 });
        _movementGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "المستخدم", Name = "User", FillWeight = 10 });

        _movementTabPage.Controls.Add(_movementGrid);

        _mainTabControl.TabPages.Add(_stockTabPage);
        _mainTabControl.TabPages.Add(_movementTabPage);

        // Overlay panels
        _loadingPanel = CreateOverlay("جاري تحليل المخزون...");
        _emptyPanel = CreateOverlay("لا توجد بيانات مخزون");
        _emptyPanel.Visible = false;

        _errorPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.BackgroundColor, Visible = false };
        _errorLabel = new Label { Text = "حدث خطأ أثناء تحميل المخزون", Font = DesignTokens.SubheadingFont, ForeColor = DesignTokens.ErrorColor, Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter };
        _retryButton = new Button { Text = "إعادة المحاولة", Font = DesignTokens.ButtonFont, BackColor = DesignTokens.PrimaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Size = new Size(150, 40), Cursor = Cursors.Hand };
        _retryButton.Anchor = AnchorStyles.None;
        _retryButton.Click += async (s, e) => await LoadStockAsync();
        _errorPanel.Controls.Add(_retryButton);
        _errorPanel.Controls.Add(_errorLabel);

        _permissionPanel = CreateOverlay("ليس لديك صلاحية لإدارة المخزون");
        _permissionPanel.Visible = false;

        Controls.Add(_loadingPanel);
        Controls.Add(_emptyPanel);
        Controls.Add(_errorPanel);
        Controls.Add(_permissionPanel);
        Controls.Add(_mainTabControl);
        Controls.Add(_summaryPanel);
        Controls.Add(_toolbarPanel);
    }

    private Panel CreateOverlay(string text)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.BackgroundColor };
        panel.Controls.Add(new Label { Text = text, Font = DesignTokens.SubheadingFont, ForeColor = DesignTokens.TextSecondaryColor, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });
        return panel;
    }

    private void SetState(InventoryState state)
    {
        _currentState = state;
        _loadingPanel.Visible = state == InventoryState.Loading;
        _emptyPanel.Visible = state == InventoryState.Empty;
        _errorPanel.Visible = state == InventoryState.Error;
        _permissionPanel.Visible = state == InventoryState.PermissionDenied;
        _mainTabControl.Visible = state == InventoryState.Loaded;
        _summaryPanel.Visible = state == InventoryState.Loaded;
    }

    public async Task LoadStockAsync()
    {
        SetState(InventoryState.Loading);

        try
        {
            if (_inventoryService != null)
            {
                var items = await _inventoryService.GetCurrentStockAsync();
                PopulateStockGrid(items);
            }
            else
            {
                await Task.Delay(600);
                PopulateStockGrid(GetSampleStock());
            }

            SetState(InventoryState.Loaded);
        }
        catch (UnauthorizedAccessException)
        {
            SetState(InventoryState.PermissionDenied);
        }
        catch
        {
            SetState(InventoryState.Error);
        }
    }

    private void PopulateStockGrid(List<InventoryStatusDto> items)
    {
        _stockGrid.Rows.Clear();
        var searchTerm = _searchTextBox.Text.Trim().ToLower();
        var filterIdx = _stockFilterCombo.SelectedIndex;

        var filtered = items.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(searchTerm))
            filtered = filtered.Where(i => i.ProductName.ToLower().Contains(searchTerm));

        if (filterIdx == 1) filtered = filtered.Where(i => !i.IsLowStock && i.Quantity > 0);
        else if (filterIdx == 2) filtered = filtered.Where(i => i.IsLowStock);
        else if (filterIdx == 3) filtered = filtered.Where(i => i.Quantity <= 0);

        foreach (var item in filtered)
        {
            var status = item.Quantity <= 0 ? "نفذ" : item.IsLowStock ? "منخفض" : "متوفر";
            _stockGrid.Rows.Add(item.ProductName, item.Quantity, item.ReservedQuantity, item.AvailableQuantity, item.MinStock, status, "—");
            _stockGrid.Rows[_stockGrid.Rows.Count - 1].Tag = item;
        }

        var total = items.Count;
        var low = items.Count(i => i.IsLowStock);
        var outOfStock = items.Count(i => i.Quantity <= 0);
        _totalItemsLabel.Text = $"📦 إجمالي الأصناف: {total}";
        _lowStockLabel.Text = $"⚠️ مخزون منخفض: {low}";
        _outOfStockLabel.Text = $"❌ نفذ من المخزون: {outOfStock}";
    }

    private List<InventoryStatusDto> GetSampleStock()
    {
        return new List<InventoryStatusDto>
        {
            new InventoryStatusDto(Guid.NewGuid(), "قهوة عربية", 50, 0, 50, "قطعة", 5, false),
            new InventoryStatusDto(Guid.NewGuid(), "شاي أحمر", 80, 0, 80, "قطعة", 10, false),
            new InventoryStatusDto(Guid.NewGuid(), "كرواسون", 3, 2, 1, "قطعة", 5, true),
            new InventoryStatusDto(Guid.NewGuid(), "كيك شوكولاتة", 0, 0, 0, "قطعة", 5, true),
            new InventoryStatusDto(Guid.NewGuid(), "عصير برتقال", 15, 0, 15, "قطعة", 5, false),
            new InventoryStatusDto(Guid.NewGuid(), "سندويش دجاج", 2, 1, 1, "قطعة", 5, true),
            new InventoryStatusDto(Guid.NewGuid(), "ماء معدني", 200, 0, 200, "زجاجة", 20, false),
            new InventoryStatusDto(Guid.NewGuid(), "حليب طازج", 0, 0, 0, "لتر", 10, true)
        };
    }

    private void StockGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_stockGrid.Columns[e.ColumnIndex].Name == "Status")
        {
            var text = e.Value?.ToString() ?? "";
            e.CellStyle.ForeColor = text switch
            {
                "متوفر" => DesignTokens.SuccessColor,
                "منخفض" => DesignTokens.WarningColor,
                "نفذ" => DesignTokens.ErrorColor,
                _ => DesignTokens.TextPrimaryColor
            };
            e.CellStyle.Font = new Font(DesignTokens.DataFont, FontStyle.Bold);
        }

        if (_stockGrid.Columns[e.ColumnIndex].Name == "Available")
        {
            if (e.Value is decimal avail && avail <= 0)
                e.CellStyle.ForeColor = DesignTokens.ErrorColor;
            else if (e.Value is decimal a && a < 5)
                e.CellStyle.ForeColor = DesignTokens.WarningColor;
        }
    }

    private void StockGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_stockGrid.Rows[e.RowIndex].Tag is InventoryStatusDto item)
            RequestAdjustment?.Invoke(this, item);
    }

    private void AdjustButton_Click(object? sender, EventArgs e)
    {
        if (_stockGrid.SelectedRows.Count > 0 && _stockGrid.SelectedRows[0].Tag is InventoryStatusDto item)
            RequestAdjustment?.Invoke(this, item);
        else
            RtlMessageBox.Show("يرجى اختيار منتج من القائمة", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
    }

    private void WasteButton_Click(object? sender, EventArgs e)
    {
        if (_stockGrid.SelectedRows.Count > 0 && _stockGrid.SelectedRows[0].Tag is InventoryStatusDto item)
            RequestWaste?.Invoke(this, item);
        else
            RtlMessageBox.Show("يرجى اختيار منتج من القائمة", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
    }

    public async Task LoadMovementsAsync(Guid? productId = null, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            if (_inventoryService != null)
            {
                var result = await _inventoryService.GetMovementsAsync(productId, from, to);
                _movementGrid.Rows.Clear();
                foreach (var m in result.Items)
                {
                    var typeText = m.MovementType switch { "Adjustment" => "تعديل", "Sale" => "بيع", "Purchase" => "شراء", "Waste" => "هالك", _ => m.MovementType };
                    _movementGrid.Rows.Add(m.Timestamp, m.ProductName, typeText, m.Quantity, m.BeforeQuantity, m.AfterQuantity, m.Reason ?? "—", m.UserName);
                }
            }
        }
        catch { System.Diagnostics.Trace.TraceError("[Inventory] Failed to load movements"); }
    }
}