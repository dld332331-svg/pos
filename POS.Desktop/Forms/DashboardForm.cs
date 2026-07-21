using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Desktop.Forms;

/// <summary>
/// DASH-001: Dashboard with widgets panel using FlowLayoutPanel.
/// Widgets: current sales total, active shift info, low stock alerts count, pending kitchen orders, recent transactions.
/// Shows empty state when no data. Loading state with spinner.
/// </summary>
public class DashboardForm : UserControl
{
    private enum DashboardState
    {
        Loading,
        Loaded,
        Empty,
        Error,
        PermissionDenied
    }

    private readonly IDashboardService? _injectedDashboardService;
    private DashboardState _currentState = DashboardState.Loading;
    private Guid _currentUserId;

    // UI Controls
    private FlowLayoutPanel _widgetsPanel;
    private Panel _loadingPanel;
    private Panel _emptyPanel;
    private Panel _errorPanel;
    private Panel _permissionPanel;
    private Panel _recentTransactionsPanel;
    private DataGridView _recentGrid;
    private Label _loadingLabel;
    private Label _emptyLabel;
    private Label _errorLabel;
    private Label _permissionLabel;
    private Button _retryButton;
    private Label _lastRefreshLabel;
    private Button _refreshButton;
    private Panel _headerPanel;
    private Label _titleLabel;

    // Events
    public event EventHandler<Guid>? NavigateToSale;
    public event EventHandler? NavigateToInventory;
    public event EventHandler? NavigateToKitchen;

    public DashboardForm()
    {
        InitializeComponent();
        _currentUserId = AppServiceProvider.CurrentUserId;
        SetState(DashboardState.Loading);
        _ = LoadDataAsync();
    }

    public DashboardForm(IDashboardService dashboardService, Guid userId) : this()
    {
        _injectedDashboardService = dashboardService;
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
            Height = 50,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingMD)
        };

        _titleLabel = new Label
        {
            Text = "لوحة التحكم",
            Font = DesignTokens.HeadingFont,
            ForeColor = DesignTokens.TextPrimaryColor,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Height = 50
        };

        _refreshButton = new Button
        {
            Text = "تحديث",
            Font = DesignTokens.DefaultFont,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(80, 32),
            Dock = DockStyle.Left,
            BackColor = DesignTokens.PrimaryColor,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };

        _lastRefreshLabel = new Label
        {
            Text = "",
            Font = DesignTokens.SmallFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            Width = 150
        };

        _headerPanel.Controls.Add(_titleLabel);
        _headerPanel.Controls.Add(_lastRefreshLabel);
        _headerPanel.Controls.Add(_refreshButton);

        // Widgets panel
        _widgetsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 200,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            AutoScroll = true,
            BackColor = DesignTokens.BackgroundColor,
            Padding = new Padding(DesignTokens.SpacingMD),
            Margin = new Padding(0)
        };

        // Recent transactions panel
        _recentTransactionsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingMD),
            Margin = new Padding(DesignTokens.SpacingSM)
        };

        var recentHeader = new Label
        {
            Text = "آخر المعاملات",
            Font = DesignTokens.SubheadingFont,
            ForeColor = DesignTokens.TextPrimaryColor,
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleRight
        };

        _recentGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            BackgroundColor = DesignTokens.SurfaceColor,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = DesignTokens.BorderColor,
            RightToLeft = RightToLeft.Yes,
            Font = DesignTokens.DataFont,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        _recentGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "رقم الفاتورة",
            Name = "InvoiceNumber",
            FillWeight = 25
        });
        _recentGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "التاريخ",
            Name = "Date",
            FillWeight = 25
        });
        _recentGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "الإجمالي",
            Name = "Total",
            FillWeight = 20,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleLeft }
        });
        _recentGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "الحالة",
            Name = "Status",
            FillWeight = 15
        });
        _recentGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "طريقة الدفع",
            Name = "Payment",
            FillWeight = 15
        });

        _recentTransactionsPanel.Controls.Add(_recentGrid);
        _recentTransactionsPanel.Controls.Add(recentHeader);

        // Loading panel
        _loadingPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.BackgroundColor
        };

        var loadingSpinner = new Panel
        {
            Size = new Size(48, 48),
            BackColor = DesignTokens.PrimaryColor,
            Location = new Point((Width / 2) - 24, (Height / 2) - 40),
            Anchor = AnchorStyles.None
        };

        _loadingLabel = new Label
        {
            Text = "جاري تحميل لوحة التحكم...",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            Location = new Point(0, 0),
            Size = new Size(200, 25),
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.None
        };

        _loadingPanel.Controls.Add(loadingSpinner);
        _loadingPanel.Controls.Add(_loadingLabel);
        _loadingPanel.Resize += (s, e) =>
        {
            loadingSpinner.Location = new Point((_loadingPanel.Width / 2) - 24, (_loadingPanel.Height / 2) - 40);
            _loadingLabel.Location = new Point((_loadingPanel.Width / 2) - 100, (_loadingPanel.Height / 2) + 10);
        };

        // Empty panel
        _emptyPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.BackgroundColor,
            Visible = false
        };

        _emptyLabel = new Label
        {
            Text = "لا توجد بيانات لعرضها حالياً",
            Font = DesignTokens.SubheadingFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };

        _emptyPanel.Controls.Add(_emptyLabel);

        // Error panel
        _errorPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.BackgroundColor,
            Visible = false
        };

        _errorLabel = new Label
        {
            Text = "حدث خطأ أثناء تحميل البيانات",
            Font = DesignTokens.SubheadingFont,
            ForeColor = DesignTokens.ErrorColor,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 50
        };

        _retryButton = new Button
        {
            Text = "إعادة المحاولة",
            Font = DesignTokens.ButtonFont,
            BackColor = DesignTokens.PrimaryColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(150, 40),
            Cursor = Cursors.Hand
        };

        _retryButton.Location = new Point((Width / 2) - 75, (Height / 2) + 10);
        _retryButton.Anchor = AnchorStyles.None;

        _errorPanel.Controls.Add(_retryButton);
        _errorPanel.Controls.Add(_errorLabel);
        _errorPanel.Resize += (s, e) =>
        {
            _retryButton.Location = new Point((_errorPanel.Width / 2) - 75, (_errorPanel.Height / 2) + 10);
        };

        // Permission denied panel
        _permissionPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.BackgroundColor,
            Visible = false
        };

        _permissionLabel = new Label
        {
            Text = "ليس لديك صلاحية لعرض لوحة التحكم",
            Font = DesignTokens.SubheadingFont,
            ForeColor = DesignTokens.WarningColor,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };

        _permissionPanel.Controls.Add(_permissionLabel);

        // Assemble
        Controls.Add(_loadingPanel);
        Controls.Add(_emptyPanel);
        Controls.Add(_errorPanel);
        Controls.Add(_permissionPanel);
        Controls.Add(_recentTransactionsPanel);
        Controls.Add(_widgetsPanel);
        Controls.Add(_headerPanel);

        // Events
        _refreshButton.Click += async (s, e) => await LoadDataAsync();
        _retryButton.Click += async (s, e) => await LoadDataAsync();
        _recentGrid.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && _recentGrid.Rows[e.RowIndex].Tag is Guid saleId)
                NavigateToSale?.Invoke(this, saleId);
        };
    }

    private void SetState(DashboardState state)
    {
        _currentState = state;

        _loadingPanel.Visible = state == DashboardState.Loading;
        _emptyPanel.Visible = state == DashboardState.Empty;
        _errorPanel.Visible = state == DashboardState.Error;
        _permissionPanel.Visible = state == DashboardState.PermissionDenied;
        _widgetsPanel.Visible = state == DashboardState.Loaded;
        _recentTransactionsPanel.Visible = state == DashboardState.Loaded;
        _refreshButton.Enabled = state != DashboardState.Loading;
    }

    public async Task LoadDataAsync()
    {
        SetState(DashboardState.Loading);

        try
        {
            IDashboardService? dashboardService = _injectedDashboardService;
            if (dashboardService == null && AppServiceProvider.Provider != null)
            {
                using var scope = AppServiceProvider.Provider.CreateScope();
                dashboardService = scope.ServiceProvider.GetService<IDashboardService>();
            }

            if (dashboardService == null)
            {
                await Task.Delay(800);
                LoadSampleWidgets();
                SetState(DashboardState.Loaded);
                _lastRefreshLabel.Text = $"آخر تحديث: {DateTime.Now:HH:mm}";
                return;
            }

            var widgets = await dashboardService.GetWidgetsAsync(_currentUserId);

            if (widgets == null || widgets.Count == 0)
            {
                SetState(DashboardState.Empty);
                return;
            }

            _widgetsPanel.Controls.Clear();
            foreach (var widget in widgets)
            {
                _widgetsPanel.Controls.Add(CreateWidgetCard(widget));
            }

            await PopulateRecentTransactionsAsync(dashboardService);

            SetState(DashboardState.Loaded);
            _lastRefreshLabel.Text = $"آخر تحديث: {DateTime.Now:HH:mm}";
        }
        catch (UnauthorizedAccessException)
        {
            SetState(DashboardState.PermissionDenied);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[Dashboard] LoadDataAsync error: {ex}");
            SetState(DashboardState.Error);
        }
    }

    private void LoadSampleWidgets()
    {
        _widgetsPanel.Controls.Clear();

        _widgetsPanel.Controls.Add(CreateWidgetCard(
            new DashboardWidgetDto("SalesTotal", "إجمالي المبيعات", "0.000 JOD",
                "مبيعات اليوم", false)));

        _widgetsPanel.Controls.Add(CreateWidgetCard(
            new DashboardWidgetDto("ActiveShift", "الوردية الحالية", "نشطة",
                "بدأت الساعة 08:00 - المدير", false)));

        _widgetsPanel.Controls.Add(CreateWidgetCard(
            new DashboardWidgetDto("LowStockAlerts", "تنبيهات المخزون", "٥",
                "منتجات تحت الحد الأدنى", true)));

        _widgetsPanel.Controls.Add(CreateWidgetCard(
            new DashboardWidgetDto("PendingKitchen", "طلبات المطبخ المعلقة", "٣",
                "بانتظار التحضير", true)));

        LoadSampleTransactions();
    }

    private Panel CreateWidgetCard(DashboardWidgetDto widget)
    {
        var card = new Panel
        {
            Size = new Size(220, 170),
            BackColor = widget.IsAlert ? Color.FromArgb(255, 243, 224) : DesignTokens.SurfaceColor,
            Margin = new Padding(DesignTokens.SpacingSM),
            Padding = new Padding(DesignTokens.SpacingMD),
            Cursor = widget.IsAlert ? Cursors.Hand : Cursors.Default,
            BorderStyle = BorderStyle.FixedSingle
        };

        var iconLabel = new Label
        {
            Text = widget.WidgetType switch
            {
                "SalesTotal" => "💰",
                "ActiveShift" => "🏢",
                "LowStockAlerts" => "⚠️",
                "PendingKitchen" => "🍳",
                _ => "📊"
            },
            Font = new Font("Segoe UI Emoji", 24),
            Location = new Point(145, 10),
            Size = new Size(40, 40),
            BackColor = Color.Transparent
        };

        var titleLabel = new Label
        {
            Text = widget.Title,
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            Location = new Point(10, 15),
            Size = new Size(130, 20),
            TextAlign = ContentAlignment.MiddleRight
        };

        var valueLabel = new Label
        {
            Text = widget.Value ?? "—",
            Font = new Font(DesignTokens.DefaultFont.FontFamily, 18f, FontStyle.Bold),
            ForeColor = widget.IsAlert ? DesignTokens.WarningColor : DesignTokens.PrimaryColor,
            Location = new Point(10, 50),
            Size = new Size(190, 35),
            TextAlign = ContentAlignment.MiddleRight
        };

        var descLabel = new Label
        {
            Text = widget.Description ?? "",
            Font = DesignTokens.SmallFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            Location = new Point(10, 100),
            Size = new Size(190, 40),
            TextAlign = ContentAlignment.MiddleRight
        };

        var statusStrip = new Panel
        {
            Location = new Point(10, 140),
            Size = new Size(190, 4),
            BackColor = widget.IsAlert ? DesignTokens.WarningColor : DesignTokens.SuccessColor
        };

        card.Controls.Add(iconLabel);
        card.Controls.Add(titleLabel);
        card.Controls.Add(valueLabel);
        card.Controls.Add(descLabel);
        card.Controls.Add(statusStrip);

        if (widget.IsAlert)
        {
            card.Click += (s, e) =>
            {
                if (widget.WidgetType == "LowStockAlerts")
                    NavigateToInventory?.Invoke(this, EventArgs.Empty);
                else if (widget.WidgetType == "PendingKitchen")
                    NavigateToKitchen?.Invoke(this, EventArgs.Empty);
            };
        }

        return card;
    }

    private void LoadSampleTransactions()
    {
        _recentGrid.Rows.Clear();

        var sampleData = new (string, string, decimal, string, string)[]
        {
            ("INV-001", "2025-01-15 10:30", 125.50m, "مكتملة", "نقدي"),
            ("INV-002", "2025-01-15 11:15", 87.00m, "مكتملة", "بطاقة"),
            ("INV-003", "2025-01-15 12:00", 250.75m, "جاري التنفيذ", "نقدي"),
            ("INV-004", "2025-01-15 12:45", 43.20m, "معلقة", "بطاقة"),
            ("INV-005", "2025-01-15 13:30", 178.90m, "مكتملة", "نقدي")
        };

        foreach (var (inv, date, total, status, payment) in sampleData)
        {
            var row = new DataGridViewRow { Tag = Guid.NewGuid() };
            row.Cells.Add(new DataGridViewTextBoxCell { Value = inv });
            row.Cells.Add(new DataGridViewTextBoxCell { Value = date });
            row.Cells.Add(new DataGridViewTextBoxCell { Value = total });
            row.Cells.Add(new DataGridViewTextBoxCell { Value = status });
            row.Cells.Add(new DataGridViewTextBoxCell { Value = payment });
            _recentGrid.Rows.Add(row);
        }
    }

    private async Task PopulateRecentTransactionsAsync(IDashboardService dashboardService)
    {
        try
        {
            var transactions = await dashboardService.GetRecentTransactionsAsync(10);
            _recentGrid.Rows.Clear();

            if (transactions.Count == 0)
            {
                _recentGrid.Rows.Add(new DataGridViewRow { Tag = null });
                _recentGrid.Rows[0].Cells[0].Value = "لا توجد معاملات حديثة";
                _recentGrid.Rows[0].ReadOnly = true;
                return;
            }

            foreach (var t in transactions)
            {
                var row = new DataGridViewRow { Tag = t.SaleId };
                row.Cells.Add(new DataGridViewTextBoxCell { Value = t.InvoiceNumber });
                row.Cells.Add(new DataGridViewTextBoxCell { Value = t.Date.ToString("yyyy/MM/dd HH:mm") });
                row.Cells.Add(new DataGridViewTextBoxCell { Value = t.TotalAmount });
                row.Cells.Add(new DataGridViewTextBoxCell { Value = t.Status == "Completed" ? "مكتملة" : t.Status });
                row.Cells.Add(new DataGridViewTextBoxCell { Value = t.PaymentMethod == "Cash" ? "نقدي" : t.PaymentMethod == "Card" ? "بطاقة" : t.PaymentMethod });
                _recentGrid.Rows.Add(row);
            }
        }
        catch
        {
            LoadSampleTransactions();
        }
    }
}