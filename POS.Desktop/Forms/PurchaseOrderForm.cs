using System.Drawing;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Desktop.Themes;
using POS.Desktop.CustomControls;

namespace POS.Desktop.Forms;

/// <summary>
/// PURCH-001: Purchase order management UserControl.
/// Top: "إنشاء أمر شراء" button + filter by supplier combo + status filter + date range.
/// Main: RtlDataGridView: رقم الأمر, المورد, التاريخ, المبلغ الإجمالي, الحالة, إجراءات.
/// Add/Edit Purchase Order dialog (large RtlDialog, width=700, height=500).
/// Receive purchase dialog. Status workflow: New → PartiallyReceived → Received → Cancelled.
/// All states: loading, empty, error. Arabic RTL.
/// </summary>
public class PurchaseOrderForm : UserControl
{
    // --- State Machine ---
    private enum FormState
    {
        Loading,
        Loaded,
        Empty,
        Error,
        PermissionDenied
    }

    private FormState _currentState = FormState.Loading;
    private readonly IPurchaseOrderService _purchaseOrderService;
    private readonly IInventoryService _inventoryService;
    private readonly ISupplierService _supplierService;
    private List<PurchaseOrderEntry> _orders = new();
    private List<PurchaseOrderEntry> _filteredOrders = new();
    private string? _filterStatus;
    private int? _filterSupplierId;
    private DateTime? _filterDateFrom;
    private DateTime? _filterDateTo;

    // UI Controls - Toolbar
    private Panel _toolbarPanel = null!;
    private RtlButton _btnCreateOrder = null!;
    private RtlButton _btnRefresh = null!;
    private RtlComboBox _cboSupplierFilter = null!;
    private RtlComboBox _cboStatusFilter = null!;
    private DateTimePicker _dtpDateFrom = null!;
    private DateTimePicker _dtpDateTo = null!;
    private Label _lblCount = null!;

    // UI Controls - Data Grid
    private RtlDataGridView _ordersGrid = null!;

    // UI Controls - Overlays
    private Panel _loadingOverlay = null!;
    private Panel _emptyOverlay = null!;
    private Panel _errorOverlay = null!;
    private Label _errorMessage = null!;
    private Panel _permissionPanel = null!;

    // Events
    public event EventHandler<int>? OrderSelected;
    public event EventHandler? OrderCreated;
    public event EventHandler? OrderReceived;

    public PurchaseOrderForm(IPurchaseOrderService purchaseOrderService, IInventoryService inventoryService, ISupplierService supplierService)
    {
        _purchaseOrderService = purchaseOrderService;
        _inventoryService = inventoryService;
        _supplierService = supplierService;
        InitializeComponent();
        SetState(FormState.Loading);
        _ = LoadDataAsync();
    }

    private void InitializeComponent()
    {
        RightToLeft = RightToLeft.Yes;
        BackColor = DesignTokens.Colors.Background;
        Font = DesignTokens.Typography.Body;
        Dock = DockStyle.Fill;

        // === Toolbar Panel ===
        _toolbarPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = DesignTokens.ControlHeight.Large + DesignTokens.Spacing.Compact + DesignTokens.Spacing.Standard,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard),
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Compact)
        };

        // Row 1: Buttons + Status filter
        var toolbarRow1 = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = DesignTokens.ControlHeight.Standard,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Small)
        };

        _btnCreateOrder = new RtlButton
        {
            Text = "➕ إنشاء أمر شراء",
            Type = RtlButton.ButtonType.Primary,
            Width = 160,
            Height = DesignTokens.ControlHeight.Standard
        };
        _btnCreateOrder.Click += (s, e) => ShowPurchaseOrderDialog(null);

        _btnRefresh = new RtlButton
        {
            Text = "🔄 تحديث",
            Type = RtlButton.ButtonType.Ghost,
            Width = 90,
            Height = DesignTokens.ControlHeight.Standard,
            Margin = new Padding(DesignTokens.Spacing.Small, 0, 0, 0)
        };
        _btnRefresh.Click += async (s, e) => await LoadDataAsync();

        _lblCount = new Label
        {
            Text = "أوامر الشراء: ٠",
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.TextSecondary,
            AutoSize = true,
            Margin = new Padding(DesignTokens.Spacing.Standard, 0, DesignTokens.Spacing.Standard, 0)
        };

        var lblStatusFilter = new Label
        {
            Text = "الحالة:",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextSecondary,
            AutoSize = true,
            Margin = new Padding(DesignTokens.Spacing.Standard, 0, DesignTokens.Spacing.Micro, 0)
        };

        _cboStatusFilter = new RtlComboBox
        {
            Width = 140,
            Height = DesignTokens.ControlHeight.Standard,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 0, DesignTokens.Spacing.Compact, 0)
        };
        _cboStatusFilter.Items.AddRange(new object[] { "الكل", "جديد", "مستلم جزئياً", "مستلم", "ملغي" });
        _cboStatusFilter.SelectedIndex = 0;
        _cboStatusFilter.SelectedIndexChanged += (s, e) =>
        {
            _filterStatus = _cboStatusFilter.SelectedIndex switch
            {
                1 => "Pending",
                2 => "PartiallyReceived",
                3 => "Received",
                4 => "Cancelled",
                _ => null
            };
            ApplyFilter();
        };

        toolbarRow1.Controls.Add(_btnCreateOrder);
        toolbarRow1.Controls.Add(_btnRefresh);
        toolbarRow1.Controls.Add(_lblCount);
        toolbarRow1.Controls.Add(lblStatusFilter);
        toolbarRow1.Controls.Add(_cboStatusFilter);

        // Row 2: Supplier filter + date range
        var toolbarRow2 = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = DesignTokens.ControlHeight.Standard,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var lblSupplierFilter = new Label
        {
            Text = "المورد:",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextSecondary,
            AutoSize = true,
            Margin = new Padding(DesignTokens.Spacing.Standard, 0, DesignTokens.Spacing.Micro, 0)
        };

        _cboSupplierFilter = new RtlComboBox
        {
            Width = 180,
            Height = DesignTokens.ControlHeight.Standard,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 0, DesignTokens.Spacing.Compact, 0)
        };
        _cboSupplierFilter.Items.Add("الكل الموردين");
        _cboSupplierFilter.SelectedIndex = 0;
        _cboSupplierFilter.SelectedIndexChanged += (s, e) =>
        {
            _filterSupplierId = _cboSupplierFilter.SelectedIndex > 0 ? _cboSupplierFilter.SelectedIndex : (int?)null;
            ApplyFilter();
        };

        var lblDateFrom = new Label
        {
            Text = "من:",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextSecondary,
            AutoSize = true,
            Margin = new Padding(DesignTokens.Spacing.Standard, 0, DesignTokens.Spacing.Micro, 0)
        };

        _dtpDateFrom = new DateTimePicker
        {
            Width = 140,
            Height = DesignTokens.ControlHeight.Standard,
            RightToLeft = RightToLeft.Yes,
            Format = DateTimePickerFormat.Short,
            Font = DesignTokens.Typography.Input
        };

        var lblDateTo = new Label
        {
            Text = "إلى:",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextSecondary,
            AutoSize = true,
            Margin = new Padding(DesignTokens.Spacing.Standard, 0, DesignTokens.Spacing.Micro, 0)
        };

        _dtpDateTo = new DateTimePicker
        {
            Width = 140,
            Height = DesignTokens.ControlHeight.Standard,
            RightToLeft = RightToLeft.Yes,
            Format = DateTimePickerFormat.Short,
            Font = DesignTokens.Typography.Input
        };

        var btnFilterDates = new RtlButton
        {
            Text = "بحث",
            Type = RtlButton.ButtonType.Secondary,
            Width = 70,
            Height = DesignTokens.ControlHeight.Standard
        };
        btnFilterDates.Click += (s, e) =>
        {
            _filterDateFrom = _dtpDateFrom.Value.Date;
            _filterDateTo = _dtpDateTo.Value.Date.AddDays(1);
            ApplyFilter();
        };

        toolbarRow2.Controls.Add(lblSupplierFilter);
        toolbarRow2.Controls.Add(_cboSupplierFilter);
        toolbarRow2.Controls.Add(lblDateTo);
        toolbarRow2.Controls.Add(_dtpDateTo);
        toolbarRow2.Controls.Add(lblDateFrom);
        toolbarRow2.Controls.Add(_dtpDateFrom);
        toolbarRow2.Controls.Add(btnFilterDates);

        _toolbarPanel.Controls.Add(toolbarRow2);
        _toolbarPanel.Controls.Add(toolbarRow1);

        // === Data Grid ===
        _ordersGrid = new RtlDataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true
        };

        _ordersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "رقم الأمر", Name = "OrderNumber", FillWeight = 12 });
        _ordersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "المورد", Name = "Supplier", FillWeight = 22 });
        _ordersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "التاريخ", Name = "Date", FillWeight = 18 });
        _ordersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "المبلغ الإجمالي", Name = "Total", FillWeight = 15 });
        _ordersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الحالة", Name = "Status", FillWeight = 13 });
        _ordersGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "إجراءات", Name = "Actions", FillWeight = 12, Text = "إجراءات", UseColumnTextForButtonValue = true });

        _ordersGrid.CellClick += OrdersGrid_CellClick;
        _ordersGrid.CellFormatting += OrdersGrid_CellFormatting;
        _ordersGrid.SelectionChanged += (s, e) =>
        {
            if (_ordersGrid.SelectedRows.Count > 0)
            {
                var order = _ordersGrid.SelectedRows[0].Tag as PurchaseOrderEntry;
                if (order != null)
                    OrderSelected?.Invoke(this, order.Id);
            }
        };

        // === Loading Overlay ===
        _loadingOverlay = ThemeManager.CreateLoadingPanel("جاري تحميل أوامر الشراء...");
        _loadingOverlay.Visible = false;

        // === Empty Overlay ===
        _emptyOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Background,
            Visible = false
        };
        var emptyIcon = new Label
        {
            Text = "📦",
            Font = new Font("Segoe UI", 48f),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 80
        };
        var emptyLabel = new Label
        {
            Text = "لا يوجد أوامر شراء",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        var emptySubLabel = new Label
        {
            Text = "اضغط على \"إنشاء أمر شراء\" لإنشاء أمر جديد",
            Font = DesignTokens.Typography.Secondary,
            ForeColor = DesignTokens.Colors.TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Bottom,
            Height = 30
        };
        _emptyOverlay.Controls.Add(emptySubLabel);
        _emptyOverlay.Controls.Add(emptyLabel);
        _emptyOverlay.Controls.Add(emptyIcon);

        // === Error Overlay ===
        _errorOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Background,
            Visible = false
        };
        var errorIcon = new Label
        {
            Text = "⚠️",
            Font = new Font("Segoe UI", 48f),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 80
        };
        _errorMessage = new Label
        {
            Text = "حدث خطأ أثناء تحميل أوامر الشراء",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.Error,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        var btnRetry = new RtlButton
        {
            Text = "🔄 إعادة المحاولة",
            Type = RtlButton.ButtonType.Primary,
            Width = 160,
            Height = DesignTokens.ControlHeight.Standard,
            Dock = DockStyle.Bottom
        };
        btnRetry.Click += async (s, e) => await LoadDataAsync();
        _errorOverlay.Controls.Add(btnRetry);
        _errorOverlay.Controls.Add(_errorMessage);
        _errorOverlay.Controls.Add(errorIcon);

        _permissionPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _permissionPanel.Controls.Add(new Label { Text = "ليس لديك صلاحية لإدارة أوامر الشراء", Font = DesignTokens.Typography.SectionTitle, ForeColor = DesignTokens.Colors.TextSecondary, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });

        // Assemble
        Controls.Add(_loadingOverlay);
        Controls.Add(_emptyOverlay);
        Controls.Add(_errorOverlay);
        Controls.Add(_permissionPanel);
        Controls.Add(_ordersGrid);
        Controls.Add(_toolbarPanel);
    }

    // --- State Management ---

    private void SetState(FormState state)
    {
        _currentState = state;
        _loadingOverlay.Visible = state == FormState.Loading;
        _emptyOverlay.Visible = state == FormState.Empty;
        _errorOverlay.Visible = state == FormState.Error;
        _permissionPanel.Visible = state == FormState.PermissionDenied;
        _ordersGrid.Visible = state == FormState.Loaded;
        _btnCreateOrder.Enabled = state == FormState.Loaded;
        _btnRefresh.Enabled = state != FormState.Loading;
    }

    // --- Data Loading ---

    private async Task LoadDataAsync()
    {
        SetState(FormState.Loading);
        try
        {
            var orders = await _purchaseOrderService.GetPurchaseOrdersAsync();
            _orders = orders.Select(o => new PurchaseOrderEntry
            {
                Id = 0,
                OrderId = o.Id,
                OrderNumber = o.OrderNumber,
                SupplierName = o.SupplierName,
                Date = o.CreatedAt,
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                Notes = o.Notes ?? "",
                Items = o.Items.Select(i => new POItem
                {
                    InventoryItemId = i.InventoryItemId,
                    ProductName = i.ItemName,
                    Quantity = (int)i.Quantity,
                    UnitCost = i.UnitCost,
                    TotalCost = i.TotalCost,
                    ReceivedQty = (int)i.ReceivedQuantity
                }).ToList()
            }).ToList();

            PopulateSupplierFilter();
            ApplyFilter();
            SetState(_filteredOrders.Count > 0 ? FormState.Loaded : FormState.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[PurchaseOrderForm] LoadDataAsync failed: {ex}");
            _errorMessage.Text = "حدث خطأ أثناء تحميل أوامر الشراء";
            SetState(FormState.Error);
        }
    }

private void PopulateSupplierFilter()
    {
        _cboSupplierFilter.Items.Clear();
        _cboSupplierFilter.Items.Add("الكل الموردين");
        foreach (var supplier in _orders.Select(o => o.SupplierName).Distinct().OrderBy(n => n))
        {
            _cboSupplierFilter.Items.Add(supplier);
        }
        _cboSupplierFilter.SelectedIndex = 0;
    }



    private void ApplyFilter()
    {
        _filteredOrders = _orders.Where(o =>
        {
            if (_filterStatus != null && o.Status != _filterStatus) return false;
            if (_filterSupplierId.HasValue && o.SupplierId != _filterSupplierId.Value)
            {
                // Filter by supplier name from combo
                if (_cboSupplierFilter.SelectedIndex > 0 && o.SupplierName != _cboSupplierFilter.Text) return false;
            }
            if (_filterDateFrom.HasValue && o.Date < _filterDateFrom.Value) return false;
            if (_filterDateTo.HasValue && o.Date >= _filterDateTo.Value) return false;
            return true;
        }).ToList();

        PopulateGrid();
        _lblCount.Text = $"أوامر الشراء: {_filteredOrders.Count}";
        SetState(_filteredOrders.Count > 0 ? FormState.Loaded : FormState.Empty);
    }

    private void PopulateGrid()
    {
        _ordersGrid.Rows.Clear();
        foreach (var order in _filteredOrders)
        {
            var statusText = PurchaseOrderCalculator.GetStatusDisplayText(order.Status);

            _ordersGrid.Rows.Add(
                order.OrderNumber,
                order.SupplierName,
                order.Date.ToString("yyyy/MM/dd"),
                DesignTokens.FormatJOD(order.TotalAmount) + " JOD",
                statusText,
                "إجراءات"
            );
            _ordersGrid.Rows[_ordersGrid.Rows.Count - 1].Tag = order;
        }
        _ordersGrid.ShowEmptyMessage("لا يوجد أوامر شراء");
    }

    // --- Cell Formatting ---

    private void OrdersGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;

        var order = _ordersGrid.Rows[e.RowIndex].Tag as PurchaseOrderEntry;
        if (order == null) return;

        if (_ordersGrid.Columns[e.ColumnIndex].Name == "Status")
        {
            e.CellStyle.ForeColor = order.Status switch
            {
                "Pending" => DesignTokens.Colors.Info,
                "PartiallyReceived" => DesignTokens.Colors.Warning,
                "Received" => DesignTokens.Colors.Success,
                "Cancelled" => DesignTokens.Colors.Error,
                _ => DesignTokens.Colors.TextPrimary
            };
            e.CellStyle.Font = new Font(DesignTokens.Typography.Table, FontStyle.Bold);
        }

        if (_ordersGrid.Columns[e.ColumnIndex].Name == "Total")
        {
            e.CellStyle.ForeColor = DesignTokens.Colors.TextPrimary;
            e.CellStyle.Font = new Font(DesignTokens.Typography.Table, FontStyle.Bold);
        }
    }

    // --- Event Handlers ---

    private void OrdersGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_ordersGrid.Columns[e.ColumnIndex].Name != "Actions") return;

        var order = _ordersGrid.Rows[e.RowIndex].Tag as PurchaseOrderEntry;
        if (order == null) return;

        var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };

        var editItem = new ToolStripMenuItem("✏️ تعديل");
        editItem.Enabled = order.Status == "Pending";
        editItem.Click += (s, e) => ShowPurchaseOrderDialog(order);
        menu.Items.Add(editItem);

        var receiveItem = new ToolStripMenuItem("📥 استلام");
        receiveItem.Enabled = order.Status == "Pending" || order.Status == "PartiallyReceived";
        receiveItem.Click += (s, e) => ShowReceiveDialog(order);
        menu.Items.Add(receiveItem);

        var printItem = new ToolStripMenuItem("🖨 طباعة");
        printItem.Click += (s, e) => { /* Print logic via event */ };
        menu.Items.Add(printItem);

        menu.Items.Add(new ToolStripSeparator());

        var cancelItem = new ToolStripMenuItem("❌ إلغاء الأمر");
        cancelItem.Enabled = order.Status == "Pending" || order.Status == "PartiallyReceived";
        cancelItem.Click += (s, e) => _ = CancelOrderAsync(order);
        menu.Items.Add(cancelItem);

        var cellRect = _ordersGrid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
        menu.Show(_ordersGrid, cellRect.Left, cellRect.Bottom);
    }

    // --- Add/Edit Purchase Order Dialog ---

    private void ShowPurchaseOrderDialog(PurchaseOrderEntry? existing)
    {
        var isEdit = existing != null;
        Label? lblTotal = null;
        var dialog = new RtlDialog(
            isEdit ? $"تعديل أمر الشراء: {existing!.OrderNumber}" : "إنشاء أمر شراء جديد",
            700, 500);

        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard)
        };

        // === Header Section ===
        var headerPanel = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 3,
            Dock = DockStyle.Top,
            Height = 120,
            RightToLeft = RightToLeft.Yes,
            BackColor = DesignTokens.Colors.Surface,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) headerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        headerPanel.Controls.Add(CreateLabel("المورد *:"), 0, 0);
        var cboSupplier = new RtlComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Height = DesignTokens.ControlHeight.Standard,
            DisplayMember = "Name",
            ValueMember = "Id"
        };

        // Load suppliers from service
        _ = LoadSuppliersAsync(cboSupplier, existing?.SupplierName ?? null);
        headerPanel.Controls.Add(cboSupplier, 1, 0);

        headerPanel.Controls.Add(CreateLabel("تاريخ الأمر:"), 0, 1);
        var dtpOrderDate = new DateTimePicker
        {
            Value = existing?.Date ?? DateTime.Now,
            Format = DateTimePickerFormat.Short,
            RightToLeft = RightToLeft.Yes,
            Font = DesignTokens.Typography.Input,
            Dock = DockStyle.Fill
        };
        headerPanel.Controls.Add(dtpOrderDate, 1, 1);

        headerPanel.Controls.Add(CreateLabel("ملاحظات:"), 0, 2);
        var txtNotes = new RtlTextBox
        {
            Text = existing?.Notes ?? "",
            Dock = DockStyle.Fill,
            PlaceholderText = "ملاحظات اختيارية..."
        };
        headerPanel.Controls.Add(txtNotes, 1, 2);

        // === Items Section ===
        var itemsLabel = new Label
        {
            Text = "بنود الأمر",
            Font = DesignTokens.Typography.CardTitle,
            ForeColor = DesignTokens.Colors.TextPrimary,
            Dock = DockStyle.Top,
            Height = 28,
            Margin = new Padding(0, DesignTokens.Spacing.Standard, 0, DesignTokens.Spacing.Micro),
            TextAlign = ContentAlignment.MiddleRight
        };

        var itemsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Background,
            Padding = new Padding(DesignTokens.Spacing.Micro)
        };

        var itemsGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes,
            BackgroundColor = DesignTokens.Colors.Surface,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Font = DesignTokens.Typography.Table,
            ColumnHeadersHeight = 36,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        itemsGrid.RowTemplate.Height = 32;

        itemsGrid.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "المنتج", Name = "Product", FillWeight = 30, Items = { "حليب كامل الدسم", "جبنة بيضاء", "لبن رائب", "قشطة", "زيت زيتون", "عدس", "سكر", "شاي" } });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الكمية", Name = "Quantity", FillWeight = 15, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0", Alignment = DataGridViewContentAlignment.MiddleCenter } });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "التكلفة الواحدة", Name = "UnitCost", FillWeight = 20, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.000", Alignment = DataGridViewContentAlignment.MiddleCenter } });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "التكلفة الإجمالية", Name = "TotalCost", FillWeight = 20, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.000", Alignment = DataGridViewContentAlignment.MiddleCenter } });
        itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الكمية المستلمة", Name = "ReceivedQty", FillWeight = 15, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0", Alignment = DataGridViewContentAlignment.MiddleCenter } });

        itemsGrid.CellValueChanged += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            var row = itemsGrid.Rows[e.RowIndex];
            if (e.ColumnIndex == row.Cells.Cast<DataGridViewCell>().ToList().FindIndex(c => c.OwningColumn?.Name == "Quantity") ||
                e.ColumnIndex == row.Cells.Cast<DataGridViewCell>().ToList().FindIndex(c => c.OwningColumn?.Name == "UnitCost"))
            {
                var qtyVal = 0m;
                var costVal = 0m;
                decimal.TryParse(row.Cells["Quantity"].Value?.ToString(), out qtyVal);
                decimal.TryParse(row.Cells["UnitCost"].Value?.ToString(), out costVal);
                row.Cells["TotalCost"].Value = PurchaseOrderCalculator.ComputeLineCost(qtyVal, costVal);
                UpdateTotalLabel(itemsGrid, lblTotal!);
            }
        };

        // Item action buttons
        var itemActionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = DesignTokens.ControlHeight.Standard,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(0, DesignTokens.Spacing.Micro, 0, 0)
        };

        var btnAddItem = new RtlButton
        {
            Text = "➕ إضافة بند",
            Type = RtlButton.ButtonType.Secondary,
            Width = 120,
            Height = DesignTokens.ControlHeight.Compact
        };
        btnAddItem.Click += (s, e) =>
        {
            itemsGrid.Rows.Add("", "1", "0.000", "0.000", "0");
            itemsGrid.CurrentCell = itemsGrid.Rows[itemsGrid.Rows.Count - 1].Cells[0];
        };

        var btnRemoveItem = new RtlButton
        {
            Text = "➖ حذف بند",
            Type = RtlButton.ButtonType.Destructive,
            Width = 120,
            Height = DesignTokens.ControlHeight.Compact
        };
        btnRemoveItem.Click += (s, e) =>
        {
            if (itemsGrid.SelectedRows.Count > 0)
                itemsGrid.Rows.RemoveAt(itemsGrid.SelectedRows[0].Index);
            UpdateTotalLabel(itemsGrid, lblTotal!);
        };

        itemActionsPanel.Controls.Add(btnAddItem);
        itemActionsPanel.Controls.Add(btnRemoveItem);

        itemsPanel.Controls.Add(itemsGrid);
        itemsPanel.Controls.Add(itemActionsPanel);

        // === Footer Total ===
        lblTotal = new Label
        {
            Text = "المجموع الإجمالي: 0.000 JOD",
            Font = DesignTokens.Typography.CardTitle,
            ForeColor = DesignTokens.Colors.Primary,
            Dock = DockStyle.Bottom,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(DesignTokens.Spacing.Standard, 0, 0, 0)
        };

        // Populate existing items
        if (isEdit && existing != null)
        {
            foreach (var item in existing.Items)
            {
                itemsGrid.Rows.Add(item.ProductName, item.Quantity, DesignTokens.FormatJOD(item.UnitCost), DesignTokens.FormatJOD(item.TotalCost), item.ReceivedQty);
            }
            UpdateTotalLabel(itemsGrid, lblTotal!);
        }

        mainPanel.Controls.Add(itemsLabel);
        mainPanel.Controls.Add(itemsPanel);
        mainPanel.Controls.Add(lblTotal);
        mainPanel.Controls.Add(headerPanel);

        // Validation label
        var lblValidation = new Label
        {
            Text = "",
            Font = DesignTokens.Typography.Secondary,
            ForeColor = DesignTokens.Colors.Error,
            Dock = DockStyle.Bottom,
            Height = 22,
            TextAlign = ContentAlignment.TopRight,
            Visible = false
        };
        mainPanel.Controls.Add(lblValidation);

        dialog.ContentArea.Controls.Add(mainPanel);

        // Dialog actions
        dialog.AddAction(isEdit ? "تحديث" : "حفظ", async (s, e) =>
        {
            // Validation
            if (cboSupplier.SelectedIndex < 0 || cboSupplier.SelectedItem is not SupplierDto selectedSupplier)
            {
                lblValidation.Text = "يرجى اختيار المورد";
                lblValidation.Visible = true;
                return;
            }

            var hasItems = itemsGrid.Rows.Cast<DataGridViewRow>().Any(r => !r.IsNewRow && r.Cells["Product"]?.Value != null && !string.IsNullOrWhiteSpace(r.Cells["Product"]?.Value?.ToString()));
            if (!hasItems)
            {
                lblValidation.Text = "يرجى إضافة بند واحد على الأقل";
                lblValidation.Visible = true;
                return;
            }

            lblValidation.Visible = false;

            // Build the order
            var poItems = new List<PurchaseOrderItemDto>();
            foreach (DataGridViewRow row in itemsGrid.Rows)
            {
                if (row.IsNewRow) continue;
                var productName = row.Cells["Product"].Value?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(productName)) continue;

                decimal.TryParse(row.Cells["Quantity"].Value?.ToString(), out var qty);
                decimal.TryParse(row.Cells["UnitCost"].Value?.ToString(), out var unitCost);

                var itemDto = new PurchaseOrderItemDto(
                    InventoryItemId: Guid.Empty,
                    ItemName: productName,
                    Quantity: qty,
                    UnitCost: unitCost,
                    TotalCost: PurchaseOrderCalculator.ComputeLineCost(qty, unitCost),
                    ReceivedQuantity: 0);
                poItems.Add(itemDto);
            }

            try
            {
                var createdOrder = await _purchaseOrderService.CreatePurchaseOrderAsync(
                    selectedSupplier.Id,
                    Guid.NewGuid(),  // userId from session
                    poItems,
                    txtNotes.Text.Trim());

                await LoadDataAsync();
                OrderCreated?.Invoke(this, EventArgs.Empty);
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[PurchaseOrderForm] CreateOrder failed: {ex}");
                lblValidation.Text = "حدث خطأ أثناء إنشاء الأمر";
                lblValidation.Visible = true;
            }
        });

        dialog.AddAction("إلغاء", (s, e) =>
        {
            dialog.DialogResult = DialogResult.Cancel;
            dialog.Close();
        }, false);

        dialog.ShowDialog(this.FindForm());
    }

    // --- Receive Purchase Dialog ---

    private void ShowReceiveDialog(PurchaseOrderEntry order)
    {
        var dialog = new RtlDialog($"استلام أمر الشراء: {order.OrderNumber}", 650, 420);

        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard)
        };

        var infoLabel = new Label
        {
            Text = $"المورد: {order.SupplierName}  |  المبلغ: {DesignTokens.FormatJOD(order.TotalAmount)} JOD",
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var receiveGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes,
            BackgroundColor = DesignTokens.Colors.Surface,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Font = DesignTokens.Typography.Table,
            ColumnHeadersHeight = 36,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        receiveGrid.RowTemplate.Height = 32;

        receiveGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "المنتج", Name = "Product", FillWeight = 20, ReadOnly = true });
        receiveGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الكمية المطلوبة", Name = "OrderedQty", FillWeight = 12, ReadOnly = true });
        receiveGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الكمية المستلمة سابقاً", Name = "PrevReceived", FillWeight = 12, ReadOnly = true });
        receiveGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الكمية المستلمة الآن", Name = "ReceiveNow", FillWeight = 12 });
        receiveGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "رقم الباتش", Name = "BatchNumber", FillWeight = 16 });
        receiveGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "تاريخ انتهاء الصلاحية (YYYY-MM-DD)", Name = "ExpiryDate", FillWeight = 14 });
        receiveGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "تاريخ التصنيع (YYYY-MM-DD)", Name = "ManufacturingDate", FillWeight = 14 });

        foreach (var item in order.Items)
        {
            var defaultBatch = $"PO-{order.OrderNumber}-{DateTime.Now:yyyyMMdd}";
            receiveGrid.Rows.Add(
                item.ProductName,
                item.Quantity,
                item.ReceivedQty,
                item.Quantity - item.ReceivedQty,
                defaultBatch,
                string.Empty,
                string.Empty
            );
        }

        mainPanel.Controls.Add(receiveGrid);
        mainPanel.Controls.Add(infoLabel);

        // Validation label
        var lblValidation = new Label
        {
            Text = "",
            Font = DesignTokens.Typography.Secondary,
            ForeColor = DesignTokens.Colors.Error,
            Dock = DockStyle.Bottom,
            Height = 22,
            TextAlign = ContentAlignment.TopRight,
            Visible = false
        };
        mainPanel.Controls.Add(lblValidation);

        dialog.ContentArea.Controls.Add(mainPanel);

        dialog.AddAction("تأكيد الاستلام", async (s, e) =>
        {
            var allValid = true;

            for (int i = 0; i < receiveGrid.Rows.Count; i++)
            {
                var row = receiveGrid.Rows[i];
                if (!int.TryParse(row.Cells["ReceiveNow"].Value?.ToString(), out var recvNow))
                {
                    recvNow = 0;
                }

                var orderedQty = order.Items[i].Quantity;
                var prevReceived = order.Items[i].ReceivedQty;
                var remaining = orderedQty - prevReceived;

                if (recvNow > remaining)
                {
                    lblValidation.Text = $"الكمية المستلمة من {order.Items[i].ProductName} ({recvNow}) تتجاوز الكمية المتبقية ({remaining})";
                    lblValidation.Visible = true;
                    allValid = false;
                    break;
                }

                order.Items[i].ReceivedQty += recvNow;
            }

            if (!allValid) return;

            // Process receive via service
            if (order.OrderId == Guid.Empty)
            {
                lblValidation.Text = "معرف الأمر الشراء غير صالح";
                lblValidation.Visible = true;
                return;
            }

            try
            {
                var batches = new List<ReceiveBatchDto>();
                for (int i = 0; i < receiveGrid.Rows.Count; i++)
                {
                    var row = receiveGrid.Rows[i];
                    var recvQty = decimal.TryParse(row.Cells["ReceiveNow"].Value?.ToString(), out var r) ? r : 0;
                    if (recvQty <= 0) continue;

                    var batchNum = row.Cells["BatchNumber"].Value?.ToString() ?? $"PO-{order.OrderNumber}-{DateTime.Now:yyyyMMdd}";
                    DateTime? expiryDate = null;
                    if (DateTime.TryParse(row.Cells["ExpiryDate"].Value?.ToString(), out var exp))
                        expiryDate = exp;
                    DateTime? mfgDate = null;
                    if (DateTime.TryParse(row.Cells["ManufacturingDate"].Value?.ToString(), out var mfg))
                        mfgDate = mfg;

                    batches.Add(new ReceiveBatchDto(
                        order.Items[i].InventoryItemId,
                        recvQty,
                        batchNum,
                        expiryDate,
                        mfgDate,
                        order.Items[i].UnitCost));
                }

                var result = await _inventoryService.ReceivePurchaseOrderWithBatchesAsync(
                    order.OrderId, Guid.NewGuid(), batches);
                if (!result.Success)
                {
                    lblValidation.Text = result.ErrorMessage ?? "فشل استلام الطلب";
                    lblValidation.Visible = true;
                    return;
                }
                await LoadDataAsync();
                OrderReceived?.Invoke(this, EventArgs.Empty);
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[PurchaseOrderForm] ReceiveOrder failed: {ex}");
                lblValidation.Text = "حدث خطأ أثناء استلام الأمر";
                lblValidation.Visible = true;
            }
        });

        dialog.AddAction("إلغاء", (s, e) =>
        {
            dialog.DialogResult = DialogResult.Cancel;
            dialog.Close();
        }, false);

        dialog.ShowDialog(this.FindForm());
    }

    // --- Cancel Order ---

    private async Task CancelOrderAsync(PurchaseOrderEntry order)
    {
        var result = RtlDialog.ShowDestructiveConfirm(
            "إلغاء أمر شراء",
            $"هل أنت متأكد من إلغاء أمر الشراء \"{order.OrderNumber}\"؟\n\nسيتم إلغاء الأمر ولا يمكن التراجع عن ذلك."
        );
        if (result == DialogResult.OK)
        {
            if (order.OrderId == Guid.Empty) return;

            try
            {
                var opResult = await _purchaseOrderService.UpdatePurchaseOrderStatusAsync(order.OrderId, "Cancelled");
                if (opResult.Success)
                    await LoadDataAsync();
            }
            catch { System.Diagnostics.Trace.TraceError("[PurchaseOrder] Failed to cancel order"); }
        }
    }

    // --- Helpers ---

    private async Task LoadSuppliersAsync(RtlComboBox combo, string? selectedName)
    {
        try
        {
            var suppliers = await _supplierService.GetSuppliersAsync();
            combo.Items.Clear();
            foreach (var supplier in suppliers)
            {
                combo.Items.Add(supplier);
            }
            if (!string.IsNullOrEmpty(selectedName))
            {
                var match = suppliers.FirstOrDefault(s => s.Name == selectedName);
                if (match != null) combo.SelectedItem = match;
            }
            else if (combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
            }
        }
        catch
        {
            // Leave combo empty if suppliers can't be loaded
        }
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

    private static void UpdateTotalLabel(DataGridView itemsGrid, Label lblTotal)
    {
        var total = 0m;
        foreach (DataGridViewRow row in itemsGrid.Rows)
        {
            if (row.IsNewRow) continue;
            var qtyVal = 0m;
            var costVal = 0m;
            decimal.TryParse(row.Cells["Quantity"].Value?.ToString(), out qtyVal);
            decimal.TryParse(row.Cells["UnitCost"].Value?.ToString(), out costVal);
            total += PurchaseOrderCalculator.ComputeLineCost(qtyVal, costVal);
        }
        lblTotal.Text = $"المجموع الإجمالي: {total:N3} JOD";
    }

    // --- Data Models ---

    public class PurchaseOrderEntry
    {
        public int Id { get; set; }
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = "";
        public string SupplierName { get; set; } = "";
        public int SupplierId { get; set; }
        public DateTime Date { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "";
        public string Notes { get; set; } = "";
        public List<POItem> Items { get; set; } = new();
    }

    public class POItem
    {
        public Guid InventoryItemId { get; set; }
        public string ProductName { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public int ReceivedQty { get; set; }
    }
}