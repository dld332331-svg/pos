using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Desktop.Forms;

/// <summary>
/// TABLE-001: Restaurant table management.
/// Shows rooms as tabs, tables as buttons in a grid layout.
/// Table button colors indicate status. Right-click context menu: Open, Transfer, Close, Merge.
/// Status legend panel.
/// </summary>
public class TableMapForm : UserControl
{
    private enum TableMapState
    {
        Loading,
        Loaded,
        Empty,
        Error,
        PermissionDenied
    }

    private readonly ITableService _tableService;
    private TableMapState _currentState = TableMapState.Loading;
    private List<TableDto> _tables = new();
    private List<RoomDto> _rooms = new();

    // UI Controls
    private Panel _toolbarPanel;
    private Button _refreshButton;
    private TabControl _roomTabControl;
    private FlowLayoutPanel _tablesGrid;
    private Panel _legendPanel;
    private Panel _loadingPanel;
    private Panel _emptyPanel;
    private Panel _errorPanel;
    private Panel _permissionPanel;

    // Events
    public event EventHandler<TableDto>? OpenTableRequested;
    public event EventHandler<TableDto>? TransferTableRequested;
    public event EventHandler<TableDto>? CloseTableRequested;
    public event EventHandler<TableDto>? MergeTableRequested;

    public TableMapForm(ITableService tableService)
    {
        _tableService = tableService;
        InitializeComponent();
        SetState(TableMapState.Loading);
        _ = LoadDataAsync();
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
            Height = 45,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingSM)
        };

        var titleLbl = new Label { Text = "🗺️ خريطة الطاولات", Font = DesignTokens.SubheadingFont, ForeColor = DesignTokens.TextPrimaryColor, Dock = DockStyle.Right, TextAlign = ContentAlignment.MiddleRight, Height = 40 };

        _refreshButton = new Button { Text = "🔄 تحديث", Font = DesignTokens.DefaultFont, FlatStyle = FlatStyle.Flat, Size = new Size(90, 32), Dock = DockStyle.Left, BackColor = DesignTokens.PrimaryColor, ForeColor = Color.White, Cursor = Cursors.Hand };
        _refreshButton.Click += async (s, e) => await LoadDataAsync();

        _toolbarPanel.Controls.Add(titleLbl);
        _toolbarPanel.Controls.Add(_refreshButton);

        // Room tabs
        _roomTabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes
        };

        // Legend
        _legendPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 35,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingSM),
            FlowDirection = FlowDirection.RightToLeft
        };

        _legendPanel.Controls.Add(CreateLegendItem("متاحة", DesignTokens.AvailableColor));
        _legendPanel.Controls.Add(CreateLegendItem("مشغولة", DesignTokens.OccupiedColor));
        _legendPanel.Controls.Add(CreateLegendItem("قيد التحضير", DesignTokens.PreparingColor));
        _legendPanel.Controls.Add(CreateLegendItem("جاهزة", DesignTokens.ReadyColor));
        _legendPanel.Controls.Add(CreateLegendItem("بانتظار الدفع", DesignTokens.WaitingForPaymentColor));
        _legendPanel.Controls.Add(CreateLegendItem("محجوزة", DesignTokens.ReservedColor));
        _legendPanel.Controls.Add(CreateLegendItem("تنظيف", DesignTokens.CleaningColor));

        // Tables grid (will be added to each tab page)
        _tablesGrid = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            AutoScroll = true,
            BackColor = DesignTokens.BackgroundColor,
            Padding = new Padding(DesignTokens.SpacingMD)
        };

        // Overlay panels
        _loadingPanel = CreateOverlay("جاري تحميل خريطة الطاولات...");
        _emptyPanel = CreateOverlay("لا توجد طاولات");
        _emptyPanel.Visible = false;

        _errorPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.BackgroundColor, Visible = false };
        var errLbl = new Label { Text = "حدث خطأ أثناء تحميل الطاولات", Font = DesignTokens.SubheadingFont, ForeColor = DesignTokens.ErrorColor, Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter };
        var retryBtn = new Button { Text = "إعادة المحاولة", Font = DesignTokens.ButtonFont, BackColor = DesignTokens.PrimaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Size = new Size(150, 40), Cursor = Cursors.Hand, Anchor = AnchorStyles.None };
        retryBtn.Click += async (s, e) => await LoadDataAsync();
        _errorPanel.Controls.Add(retryBtn);
        _errorPanel.Controls.Add(errLbl);

        _permissionPanel = CreateOverlay("ليس لديك صلاحية لإدارة الطاولات");
        _permissionPanel.Visible = false;

        Controls.Add(_loadingPanel);
        Controls.Add(_emptyPanel);
        Controls.Add(_errorPanel);
        Controls.Add(_permissionPanel);
        Controls.Add(_roomTabControl);
        Controls.Add(_legendPanel);
        Controls.Add(_toolbarPanel);
    }

    private Label CreateLegendItem(string text, Color color)
    {
        var panel = new Panel { Size = new Size(12, 12), BackColor = color, Margin = new Padding(0, 0, 4, 0) };
        var label = new Label
        {
            Text = text,
            Font = DesignTokens.SmallFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            AutoSize = true,
            Margin = new Padding(0, 0, DesignTokens.SpacingMD, 0)
        };
        label.Controls.Add(panel);
        panel.Location = new Point(label.PreferredWidth + 4, 2);
        return label;
    }

    private Panel CreateOverlay(string text)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.BackgroundColor };
        panel.Controls.Add(new Label { Text = text, Font = DesignTokens.SubheadingFont, ForeColor = DesignTokens.TextSecondaryColor, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });
        return panel;
    }

    private void SetState(TableMapState state)
    {
        _currentState = state;
        _loadingPanel.Visible = state == TableMapState.Loading;
        _emptyPanel.Visible = state == TableMapState.Empty;
        _errorPanel.Visible = state == TableMapState.Error;
        _permissionPanel.Visible = state == TableMapState.PermissionDenied;
        _roomTabControl.Visible = state == TableMapState.Loaded;
        _legendPanel.Visible = state == TableMapState.Loaded;
    }

    public async Task LoadDataAsync()
    {
        SetState(TableMapState.Loading);

        try
        {
            var tablesTask = _tableService.GetTablesAsync();
            var roomsTask = _tableService.GetRoomsAsync();
            await Task.WhenAll(tablesTask, roomsTask);
            _tables = await tablesTask;
            _rooms = await roomsTask;

            BuildRoomTabs();
            SetState(_tables.Count > 0 ? TableMapState.Loaded : TableMapState.Empty);
        }
        catch (UnauthorizedAccessException)
        {
            SetState(TableMapState.PermissionDenied);
        }
        catch
        {
            SetState(TableMapState.Error);
        }
    }

    private void BuildRoomTabs()
    {
        _roomTabControl.TabPages.Clear();

        if (_rooms.Count == 0)
        {
            var allPage = new TabPage("الكل");
            var grid = CreateTableGrid(_tables);
            allPage.Controls.Add(grid);
            _roomTabControl.TabPages.Add(allPage);
            return;
        }

        // All tab
        var allTabPage = new TabPage("الكل");
        allTabPage.Controls.Add(CreateTableGrid(_tables));
        _roomTabControl.TabPages.Add(allTabPage);

        // Per-room tabs
        foreach (var room in _rooms.OrderBy(r => r.SortOrder))
        {
            var roomTables = _tables.Where(t => t.RoomName == room.Name).ToList();
            var page = new TabPage(room.Name);
            page.Controls.Add(CreateTableGrid(roomTables));
            _roomTabControl.TabPages.Add(page);
        }
    }

    private FlowLayoutPanel CreateTableGrid(List<TableDto> tables)
    {
        var grid = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            AutoScroll = true,
            BackColor = DesignTokens.BackgroundColor,
            Padding = new Padding(DesignTokens.SpacingMD)
        };

        foreach (var table in tables)
        {
            grid.Controls.Add(CreateTableButton(table));
        }

        return grid;
    }

    private Button CreateTableButton(TableDto table)
    {
        var statusColor = table.Status switch
        {
            "Available" => DesignTokens.AvailableColor,
            "Occupied" => DesignTokens.OccupiedColor,
            "Preparing" => DesignTokens.PreparingColor,
            "Ready" => DesignTokens.ReadyColor,
            "WaitingForPayment" => DesignTokens.WaitingForPaymentColor,
            "Reserved" => DesignTokens.ReservedColor,
            "Cleaning" => DesignTokens.CleaningColor,
            _ => DesignTokens.BorderColor
        };

        var statusText = table.Status switch
        {
            "Available" => "متاحة",
            "Occupied" => "مشغولة",
            "Preparing" => "قيد التحضير",
            "Ready" => "جاهزة",
            "WaitingForPayment" => "بانتظار الدفع",
            "Reserved" => "محجوزة",
            "Cleaning" => "تنظيف",
            _ => table.Status
        };

        var btn = new Button
        {
            Text = $"{table.TableNumber}\n{statusText}\n({table.Capacity} أشخاص)",
            Font = new Font(DesignTokens.DefaultFont.FontFamily, 10f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = statusColor,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 90),
            Margin = new Padding(DesignTokens.SpacingSM),
            Cursor = Cursors.Hand,
            Tag = table,
            TextAlign = ContentAlignment.MiddleCenter
        };

        btn.Click += (s, e) =>
        {
            if (table.Status == "Available")
                OpenTableRequested?.Invoke(this, table);
        };

        btn.MouseUp += (s, e) =>
        {
            if (e.Button == MouseButtons.Right)
                ShowTableContextMenu(btn, table);
        };

        return btn;
    }

    private void ShowTableContextMenu(Control owner, TableDto table)
    {
        var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };

        if (table.Status == "Available")
        {
            menu.Items.Add("🔓 فتح الطاولة", null, (s, e) => OpenTableRequested?.Invoke(this, table));
        }
        if (table.Status is "Occupied" or "Preparing" or "Ready" or "WaitingForPayment")
        {
            menu.Items.Add("🔄 نقل الطلب", null, (s, e) => TransferTableRequested?.Invoke(this, table));
            menu.Items.Add("✅ إغلاق الطاولة", null, (s, e) => CloseTableRequested?.Invoke(this, table));
        }
        if (table.Status == "Available" || table.Status is "Occupied" or "Preparing" or "Ready" or "WaitingForPayment")
        {
            menu.Items.Add("🔗 دمج طاولة", null, (s, e) => MergeTableRequested?.Invoke(this, table));
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add($"السعة: {table.Capacity} أشخاص", null, (s, e) => { });
        menu.Items.Add($"الحالة: {table.Status}", null, (s, e) => { });

        menu.Show(owner, owner.PointToClient(Cursor.Position));
    }
}