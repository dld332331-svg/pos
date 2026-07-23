using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Application.Services;

using POS.Desktop.Themes;
namespace POS.Desktop.Forms;

/// <summary>
/// RPT-001: Reports dashboard.
/// Left: report type list (Sales, Inventory, Profitability, Cash).
/// Right: filter panel (date range, user, category) + results area (DataGridView or chart placeholder).
/// Export button, Print button. All Arabic.
/// </summary>
public class ReportForm : UserControl
{
    private enum ReportState
    {
        Idle,
        Loading,
        Loaded,
        Empty,
        Error,
        PermissionDenied
    }

    private readonly IReportService? _reportService;
    private readonly IReportExporter? _reportExporter;
    private ReportState _currentState = ReportState.Idle;
    private string _selectedReportType = "Sales";

    // UI Controls
    private Panel _leftPanel;
    private Panel _rightPanel;
    private ListBox _reportTypeList;
    private Panel _filterPanel;
    private DateTimePicker _fromDatePicker;
    private DateTimePicker _toDatePicker;
    private ComboBox _userFilterCombo;
    private ComboBox _categoryFilterCombo;
    private Button _generateButton;
    private Button _exportButton;
    private Button _printButton;
    private DataGridView _resultsGrid;
    private Panel _chartPlaceholder;
    private Panel _loadingPanel;
    private Panel _emptyPanel;
    private Panel _errorPanel;
    private Panel _permissionPanel;
    private Label _summaryLabel;
    private Panel _summaryPanel;

    // Events
    public event EventHandler? ExportRequested;
    public event EventHandler? PrintRequested;

    public ReportForm()
    {
        InitializeComponent();
        SetState(ReportState.Idle);
    }

    public ReportForm(IReportService reportService) : this()
    {
        _reportService = reportService;
    }

    public ReportForm(IReportService reportService, IReportExporter reportExporter) : this()
    {
        _reportService = reportService;
        _reportExporter = reportExporter;
        _exportButton.Click -= (s, e) => ExportRequested?.Invoke(this, EventArgs.Empty);
        _exportButton.Click += ExportReportAsync;
    }

    private void InitializeComponent()
    {
        RightToLeft = RightToLeft.Yes;
        BackColor = DesignTokens.BackgroundColor;
        Font = DesignTokens.DefaultFont;
        Dock = DockStyle.Fill;

        // === LEFT PANEL: Report Types ===
        _leftPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 180,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingSM)
        };

        var listTitle = new Label
        {
            Text = "📊 أنواع التقارير",
            Font = DesignTokens.SubheadingFont,
            ForeColor = DesignTokens.TextPrimaryColor,
            Dock = DockStyle.Top,
            Height = 35,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _reportTypeList = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = DesignTokens.DefaultFont,
            RightToLeft = RightToLeft.Yes,
            BorderStyle = BorderStyle.None,
            BackColor = DesignTokens.SurfaceColor,
            ForeColor = DesignTokens.TextPrimaryColor,
            SelectionMode = SelectionMode.One
        };

        _reportTypeList.Items.AddRange(new object[]
        {
            "💰 تقرير المبيعات",
            "📦 تقرير المخزون",
            "📈 تقرير الربحية",
            "💵 تقرير النقدية"
        });
        _reportTypeList.SelectedIndex = 0;
        _reportTypeList.SelectedIndexChanged += (s, e) =>
        {
            _selectedReportType = _reportTypeList.SelectedIndex switch
            {
                0 => "Sales", 1 => "Inventory", 2 => "Profitability", 3 => "Cash", _ => "Sales"
            };
            UpdateFiltersForReportType();
        };

        _leftPanel.Controls.Add(_reportTypeList);
        _leftPanel.Controls.Add(listTitle);

        // === RIGHT PANEL: Filters + Results ===
        _rightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.BackgroundColor,
            Padding = new Padding(DesignTokens.SpacingSM)
        };

        // Filter panel
        _filterPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingSM),
            Margin = new Padding(0, 0, 0, DesignTokens.SpacingSM)
        };

        var fromLabel = new Label { Text = "من تاريخ:", Font = DesignTokens.DefaultFont, ForeColor = DesignTokens.TextPrimaryColor, Location = new Point(460, 10), Size = new Size(80, 22), TextAlign = ContentAlignment.MiddleRight };
        _fromDatePicker = new DateTimePicker { Location = new Point(370, 8), Size = new Size(85, 26), Format = DateTimePickerFormat.Short, RightToLeft = RightToLeft.Yes, Value = DateTime.Today.AddDays(-30) };

        var toLabel = new Label { Text = "إلى تاريخ:", Font = DesignTokens.DefaultFont, ForeColor = DesignTokens.TextPrimaryColor, Location = new Point(290, 10), Size = new Size(75, 22), TextAlign = ContentAlignment.MiddleRight };
        _toDatePicker = new DateTimePicker { Location = new Point(200, 8), Size = new Size(85, 26), Format = DateTimePickerFormat.Short, RightToLeft = RightToLeft.Yes, Value = DateTime.Today };

        var userLabel = new Label { Text = "المستخدم:", Font = DesignTokens.DefaultFont, ForeColor = DesignTokens.TextPrimaryColor, Location = new Point(460, 44), Size = new Size(80, 22), TextAlign = ContentAlignment.MiddleRight };
        _userFilterCombo = new ComboBox { Location = new Point(370, 42), Size = new Size(85, 26), DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
        _userFilterCombo.Items.AddRange(new object[] { "الكل", "المدير", "كاشير ١", "كاشير ٢" });
        _userFilterCombo.SelectedIndex = 0;

        var catLabel = new Label { Text = "الفئة:", Font = DesignTokens.DefaultFont, ForeColor = DesignTokens.TextPrimaryColor, Location = new Point(290, 44), Size = new Size(75, 22), TextAlign = ContentAlignment.MiddleRight };
        _categoryFilterCombo = new ComboBox { Location = new Point(200, 42), Size = new Size(85, 26), DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
        _categoryFilterCombo.Items.AddRange(new object[] { "الكل", "مشروبات ساخنة", "مشروبات باردة", "وجبات", "حلويات" });
        _categoryFilterCombo.SelectedIndex = 0;

        _generateButton = new Button { Text = "📊 إنشاء التقرير", Font = DesignTokens.ButtonFont, FlatStyle = FlatStyle.Flat, Location = new Point(10, 15), Size = new Size(140, 50), BackColor = DesignTokens.PrimaryColor, ForeColor = Color.White, Cursor = Cursors.Hand };
        _generateButton.Click += async (s, e) => await GenerateReportAsync();

        _filterPanel.Controls.AddRange(new Control[] { fromLabel, _fromDatePicker, toLabel, _toDatePicker, userLabel, _userFilterCombo, catLabel, _categoryFilterCombo, _generateButton });

        // Action buttons
        var actionsPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = DesignTokens.BackgroundColor,
            Margin = new Padding(0, 0, 0, DesignTokens.SpacingSM)
        };

        _exportButton = new Button { Text = "📥 تصدير", Font = DesignTokens.DefaultFont, FlatStyle = FlatStyle.Flat, Size = new Size(100, 32), Dock = DockStyle.Left, BackColor = DesignTokens.SuccessColor, ForeColor = Color.White, Cursor = Cursors.Hand, Enabled = false };

        _printButton = new Button { Text = "🖨️ طباعة", Font = DesignTokens.DefaultFont, FlatStyle = FlatStyle.Flat, Size = new Size(100, 32), Dock = DockStyle.Left, BackColor = DesignTokens.InfoColor, ForeColor = Color.White, Cursor = Cursors.Hand, Enabled = false, Margin = new Padding(0, 0, DesignTokens.SpacingSM, 0) };
        _printButton.Click += (s, e) => PrintRequested?.Invoke(this, EventArgs.Empty);

        actionsPanel.Controls.Add(_exportButton);
        actionsPanel.Controls.Add(_printButton);

        // Summary panel
        _summaryPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 45,
            BackColor = DesignTokens.CardColor,
            Padding = new Padding(DesignTokens.SpacingSM),
            Margin = new Padding(0, 0, 0, DesignTokens.SpacingSM)
        };

        _summaryLabel = new Label
        {
            Text = "اختر نوع التقرير ثم اضغط إنشاء",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _summaryPanel.Controls.Add(_summaryLabel);

        // Results grid
        _resultsGrid = new DataGridView
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

        // Chart placeholder
        _chartPlaceholder = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 200,
            BackColor = DesignTokens.CardColor,
            Visible = false,
            Padding = new Padding(DesignTokens.SpacingSM)
        };

        var chartLabel = new Label
        {
            Text = "📈 مخطط بياني (سيتم عرض الرسم البياني هنا)",
            Font = DesignTokens.SubheadingFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        _chartPlaceholder.Controls.Add(chartLabel);

        // Overlay panels
        _loadingPanel = CreateOverlay("جاري إنشاء التقرير...");
        _loadingPanel.Visible = false;
        _emptyPanel = CreateOverlay("لا توجد بيانات للتقرير المحدد");
        _emptyPanel.Visible = false;
        _errorPanel = CreateOverlayError("حدث خطأ أثناء إنشاء التقرير");
        _errorPanel.Visible = false;
        _permissionPanel = CreateOverlay("ليس لديك صلاحية لعرض التقارير");
        _permissionPanel.Visible = false;

        _rightPanel.Controls.Add(_loadingPanel);
        _rightPanel.Controls.Add(_emptyPanel);
        _rightPanel.Controls.Add(_errorPanel);
        _rightPanel.Controls.Add(_permissionPanel);
        _rightPanel.Controls.Add(_chartPlaceholder);
        _rightPanel.Controls.Add(_resultsGrid);
        _rightPanel.Controls.Add(_summaryPanel);
        _rightPanel.Controls.Add(actionsPanel);
        _rightPanel.Controls.Add(_filterPanel);

        Controls.Add(_leftPanel);
        Controls.Add(_rightPanel);
    }

    private Panel CreateOverlay(string text)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.BackgroundColor };
        panel.Controls.Add(new Label { Text = text, Font = DesignTokens.SubheadingFont, ForeColor = DesignTokens.TextSecondaryColor, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });
        return panel;
    }

    private Panel CreateOverlayError(string text)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.BackgroundColor };
        var lbl = new Label { Text = text, Font = DesignTokens.SubheadingFont, ForeColor = DesignTokens.ErrorColor, Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter };
        var btn = new Button { Text = "إعادة المحاولة", Font = DesignTokens.ButtonFont, BackColor = DesignTokens.PrimaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Size = new Size(150, 40), Cursor = Cursors.Hand, Anchor = AnchorStyles.None };
        btn.Click += async (s, e) => await GenerateReportAsync();
        panel.Controls.Add(btn);
        panel.Controls.Add(lbl);
        return panel;
    }

    private void SetState(ReportState state)
    {
        _currentState = state;
        _loadingPanel.Visible = state == ReportState.Loading;
        _emptyPanel.Visible = state == ReportState.Empty;
        _errorPanel.Visible = state == ReportState.Error;
        _permissionPanel.Visible = state == ReportState.PermissionDenied;
        _resultsGrid.Visible = state == ReportState.Loaded;
        _chartPlaceholder.Visible = state == ReportState.Loaded && _selectedReportType == "Sales";
        _generateButton.Enabled = state != ReportState.Loading;
        _exportButton.Enabled = state == ReportState.Loaded;
        _printButton.Enabled = state == ReportState.Loaded;
    }

    private void UpdateFiltersForReportType()
    {
        var showUser = _selectedReportType is "Sales" or "Cash";
        var showCategory = _selectedReportType is "Sales" or "Profitability";
        _userFilterCombo.Enabled = showUser;
        _categoryFilterCombo.Enabled = showCategory;
    }

    private async Task GenerateReportAsync()
    {
        SetState(ReportState.Loading);

        try
        {
            if (_reportService != null)
            {
                var from = _fromDatePicker.Value;
                var to = _toDatePicker.Value;

                switch (_selectedReportType)
                {
                    case "Sales":
                        var salesFilter = new SalesReportFilter(from, to, null, null, null);
                        var salesReport = await _reportService.GetSalesReportAsync(salesFilter);
                        PopulateSalesReport(salesReport);
                        break;
                    case "Inventory":
                        var invReport = await _reportService.GetInventoryReportAsync();
                        PopulateInventoryReport(invReport);
                        break;
                    case "Profitability":
                        var profReport = await _reportService.GetProfitabilityReportAsync(from, to);
                        PopulateProfitabilityReport(profReport);
                        break;
                    case "Cash":
                        break;
                }
            }
            else
            {
                await Task.Delay(800);
                PopulateSampleReport();
            }

            SetState(ReportState.Loaded);
        }
        catch (UnauthorizedAccessException)
        {
            SetState(ReportState.PermissionDenied);
        }
        catch
        {
            SetState(ReportState.Error);
        }
    }

    private void PopulateSalesReport(SalesReportDto report)
    {
        _resultsGrid.Columns.Clear();
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "التاريخ", Name = "Date", FillWeight = 25 });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "المبيعات", Name = "Sales", FillWeight = 20, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3" } });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الضريبة", Name = "Tax", FillWeight = 15, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3" } });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الخصم", Name = "Discount", FillWeight = 15, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3" } });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "عدد العمليات", Name = "Count", FillWeight = 15, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _resultsGrid.Rows.Clear();

        foreach (var d in report.DailySales)
            _resultsGrid.Rows.Add(d.Date.ToString("yyyy/MM/dd"), d.TotalSales, d.TotalTax, d.TotalDiscount, d.TransactionCount);

        _summaryLabel.Text = $"الإجمالي: {report.GrandTotal:N3} JOD | الضريبة: {report.GrandTax:N3} | الخصومات: {report.GrandDiscount:N3} | العمليات: {report.TotalTransactions}";
    }

    private void PopulateInventoryReport(InventoryReportDto report)
    {
        _resultsGrid.Columns.Clear();
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "المنتج", Name = "Product", FillWeight = 30 });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الكمية", Name = "Qty", FillWeight = 15 });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "المتاح", Name = "Available", FillWeight = 15 });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الحد الأدنى", Name = "MinStock", FillWeight = 15 });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الحالة", Name = "Status", FillWeight = 15 });
        _resultsGrid.Rows.Clear();

        foreach (var item in report.Items)
            _resultsGrid.Rows.Add(item.ProductName, item.Quantity, item.AvailableQuantity, item.MinStock, item.IsLowStock ? "منخفض" : "متوفر");

        _summaryLabel.Text = $"إجمالي الأصناف: {report.TotalItems} | مخزون منخفض: {report.LowStockCount}";
        _chartPlaceholder.Visible = false;
    }

    private void PopulateProfitabilityReport(ProfitabilityReportDto report)
    {
        _resultsGrid.Columns.Clear();
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "المنتج", Name = "Product", FillWeight = 25 });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "المبيعات", Name = "Sales", FillWeight = 18, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3" } });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "التكلفة", Name = "Cost", FillWeight = 18, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3" } });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الربح", Name = "Profit", FillWeight = 18, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3" } });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الهامش %", Name = "Margin", FillWeight = 15, DefaultCellStyle = new DataGridViewCellStyle { Format = "P1" } });
        _resultsGrid.Rows.Clear();

        foreach (var p in report.TopProducts)
            _resultsGrid.Rows.Add(p.ProductName, p.Sales, p.Cost, p.Profit, p.Margin);

        _summaryLabel.Text = $"المبيعات: {report.TotalSales:N2} | التكلفة: {report.TotalCost:N2} | الربح: {report.GrossProfit:N2} | الهامش: {report.ProfitMargin:P1}";
        _chartPlaceholder.Visible = false;
    }

    private void PopulateSampleReport()
    {
        _resultsGrid.Columns.Clear();
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "التاريخ", Name = "Date", FillWeight = 25 });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "المبيعات", Name = "Sales", FillWeight = 20, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3" } });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الضريبة", Name = "Tax", FillWeight = 15, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3" } });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الخصم", Name = "Discount", FillWeight = 15, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3" } });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "عدد العمليات", Name = "Count", FillWeight = 15, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _resultsGrid.Rows.Clear();

        var rand = new Random();
        for (int i = 6; i >= 0; i--)
        {
            var date = DateTime.Today.AddDays(-i);
            var sales = rand.Next(500, 3000);
            var tax = sales * 0.15m;
            var discount = rand.Next(0, 100);
            var count = rand.Next(5, 30);
            _resultsGrid.Rows.Add(date.ToString("yyyy/MM/dd"), sales, tax, discount, count);
        }

        _summaryLabel.Text = "إجمالي المبيعات: 12,450.000 JOD | إجمالي العمليات: 95";
    }

    private async void ExportReportAsync(object? sender, EventArgs e)
    {
        try
        {
            if (_reportExporter is null || _resultsGrid.Rows.Count == 0) return;

        using var formatDialog = new Form { Text = "اختر صيغة التصدير", StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, Size = new Size(300, 160), RightToLeft = RightToLeft.Yes };
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(20), WrapContents = false };
        var lbl = new Label { Text = "اختر صيغة الملف:", Font = DesignTokens.DefaultFont, AutoSize = true };
        var pdfBtn = new Button { Text = "📄 PDF", Font = DesignTokens.ButtonFont, Size = new Size(200, 40), BackColor = DesignTokens.PrimaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        var xlsxBtn = new Button { Text = "📊 Excel", Font = DesignTokens.ButtonFont, Size = new Size(200, 40), BackColor = DesignTokens.SuccessColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };

        pdfBtn.Click += (s, args) => { formatDialog.DialogResult = DialogResult.Yes; formatDialog.Close(); };
        xlsxBtn.Click += (s, args) => { formatDialog.DialogResult = DialogResult.No; formatDialog.Close(); };

        layout.Controls.Add(xlsxBtn);
        layout.Controls.Add(pdfBtn);
        layout.Controls.Add(lbl);
        formatDialog.Controls.Add(layout);

        var result = formatDialog.ShowDialog(this);
        if (result != DialogResult.Yes && result != DialogResult.No) return;

        var isPdf = result == DialogResult.Yes;
        var columns = _resultsGrid.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText).ToArray();
        var rows = _resultsGrid.Rows.Cast<DataGridViewRow>()
            .Where(r => !r.IsNewRow)
            .Select(r => r.Cells.Cast<DataGridViewCell>().Select(c => c.Value).ToArray())
            .ToArray();
        var summary = _summaryLabel.Text;

        var data = isPdf
            ? _reportExporter.ExportToPdf(_selectedReportType + " تقرير", columns, rows, summary)
            : _reportExporter.ExportToExcel(_selectedReportType + " تقرير", columns, rows, summary);

        var extension = isPdf ? "pdf" : "xlsx";
        var filter = isPdf ? "PDF files (*.pdf)|*.pdf" : "Excel files (*.xlsx)|*.xlsx";

        using var saveDialog = new SaveFileDialog
        {
            Filter = filter,
            DefaultExt = extension,
            FileName = $"تقرير_{_selectedReportType}_{DateTime.Now:yyyyMMdd}.{extension}"
        };

        if (saveDialog.ShowDialog() == DialogResult.OK)
        {
            await File.WriteAllBytesAsync(saveDialog.FileName, data);
            RtlMessageBox.Show($"تم تصدير التقرير بنجاح إلى:\n{saveDialog.FileName}", "تصدير التقرير", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[ReportForm] ExportReportAsync failed: {ex}");
        }
    }
}