using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.Utils;
using POS.Application.Services;
using POS.Application.DTOs;
using POS.Desktop.Themes;
using POS.Desktop.CustomControls;

namespace POS.Desktop.Forms;

public class CustomerListForm : UserControl
{
    private enum CustomerState
    {
        Loading,
        Loaded,
        Empty,
        Error,
        PermissionDenied
    }

    private CustomerState _currentState = CustomerState.Loading;
    private readonly ICustomerService _customerService;
    private List<CustomerDto> _customers = new();
    private List<CustomerDto> _filteredCustomers = new();
    private string _searchText = "";

    private Panel _toolbarPanel = null!;
    private RtlTextBox _txtSearch = null!;
    private RtlButton _btnAddCustomer = null!;
    private RtlButton _btnRefresh = null!;
    private Label _lblCount = null!;
    private RtlGridControl _customersGrid = null!;
    private Panel _loadingOverlay = null!;
    private Panel _emptyOverlay = null!;
    private Panel _permissionPanel = null!;

    public event EventHandler<Guid>? CustomerSelected;

    public CustomerListForm(ICustomerService customerService)
    {
        _customerService = customerService;
        InitializeComponent();
        SetState(CustomerState.Loading);
        _ = LoadDataAsync();
    }

    private void InitializeComponent()
    {
        RightToLeft = RightToLeft.Yes;
        BackColor = DesignTokens.Colors.Background;
        Font = DesignTokens.Typography.Body;
        Dock = DockStyle.Fill;

        _toolbarPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = DesignTokens.ControlHeight.Large + DesignTokens.Spacing.Compact,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard)
        };

        var toolbarInner = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        _btnAddCustomer = new RtlButton { Text = "➕ إضافة عميل", Type = RtlButton.ButtonType.Primary, Width = 140, Height = DesignTokens.ControlHeight.Standard };
        _btnAddCustomer.Click += (s, e) => ShowCustomerDialog(null);

        _btnRefresh = new RtlButton { Text = "🔄 تحديث", Type = RtlButton.ButtonType.Ghost, Width = 90, Height = DesignTokens.ControlHeight.Standard, Margin = new Padding(DesignTokens.Spacing.Small, 0, 0, 0) };
        _btnRefresh.Click += async (s, e) => await LoadDataAsync();

        _lblCount = new Label { Text = "العملاء: ٠", Font = DesignTokens.Typography.BodyBold, ForeColor = DesignTokens.Colors.TextSecondary, AutoSize = true, Margin = new Padding(DesignTokens.Spacing.Standard, 0, DesignTokens.Spacing.Standard, 0) };

        _txtSearch = new RtlTextBox { PlaceholderText = "🔍 بحث بالاسم أو الهاتف...", Width = 280, Height = DesignTokens.ControlHeight.Standard, Margin = new Padding(0, 0, DesignTokens.Spacing.Compact, 0) };
        _txtSearch.TextChanged += (s, e) => { _searchText = _txtSearch.Text.Trim(); ApplyFilter(); };

        toolbarInner.Controls.Add(_btnAddCustomer);
        toolbarInner.Controls.Add(_btnRefresh);
        toolbarInner.Controls.Add(_lblCount);
        toolbarInner.Controls.Add(_txtSearch);
        _toolbarPanel.Controls.Add(toolbarInner);

        _customersGrid = new RtlGridControl();
        _customersGrid.AddTextColumn("Name", "الاسم", 220);
        _customersGrid.AddTextColumn("Phone", "الهاتف", 150);
        _customersGrid.AddTextColumn("Email", "البريد", 220);
        _customersGrid.AddTextColumn("Balance", "الرصيد", 130, HorzAlignment.Far, "0.000 JOD");
        _customersGrid.AddActionsColumn("إجراءات", 80);

        _customersGrid.GridViewCore.RowCellStyle += (s, e) =>
        {
            if (e.Column.FieldName == "Balance")
            {
                var raw = Convert.ToDecimal(_customersGrid.GridViewCore.GetRowCellValue(e.RowHandle, "RawBalance") ?? 0m);
                e.Appearance.ForeColor = raw < 0 ? DesignTokens.Colors.Error : raw > 0 ? DesignTokens.Colors.Success : DesignTokens.Colors.TextPrimary;
                e.Appearance.Options.UseForeColor = true;
            }
        };

        _customersGrid.ActionButtonClick += async (s, e) =>
        {
            if (e.RowData is DataRowView row)
            {
                var id = (Guid)row["__Id"];
                var c = _filteredCustomers.FirstOrDefault(x => x.Id == id);
                if (c != null) await ShowCustomerActionsMenu(c);
            }
        };

        _customersGrid.GridViewCore.FocusedRowChanged += (s, e) =>
        {
            if (e.FocusedRowHandle >= 0 && _customersGrid.GridViewCore.GetRow(e.FocusedRowHandle) is DataRowView row)
                CustomerSelected?.Invoke(this, (Guid)((DataRowView)row)["__Id"]);
        };

        _loadingOverlay = ThemeManager.CreateLoadingPanel("جاري تحميل قائمة العملاء...");
        _loadingOverlay.Visible = false;

        _emptyOverlay = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        var emptyIcon = new Label { Text = "👥", Font = new Font("Segoe UI", 48f), TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top, Height = 80 };
        var emptyLabel = new Label { Text = "لا يوجد عملاء مسجلون", Font = DesignTokens.Typography.SectionTitle, ForeColor = DesignTokens.Colors.TextSecondary, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
        _emptyOverlay.Controls.Add(emptyLabel);
        _emptyOverlay.Controls.Add(emptyIcon);

        _permissionPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _permissionPanel.Controls.Add(new Label { Text = "ليس لديك صلاحية لعرض العملاء", Font = DesignTokens.Typography.SectionTitle, ForeColor = DesignTokens.Colors.TextSecondary, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });

        Controls.Add(_loadingOverlay);
        Controls.Add(_emptyOverlay);
        Controls.Add(_permissionPanel);
        Controls.Add(_customersGrid);
        Controls.Add(_toolbarPanel);
    }

    private void SetState(CustomerState state)
    {
        _currentState = state;
        _loadingOverlay.Visible = state == CustomerState.Loading;
        _emptyOverlay.Visible = state == CustomerState.Empty;
        _permissionPanel.Visible = state == CustomerState.PermissionDenied;
        _customersGrid.Visible = state == CustomerState.Loaded;
        _btnAddCustomer.Enabled = state == CustomerState.Loaded;
    }

    private async Task LoadDataAsync()
    {
        try
        {
            SetState(CustomerState.Loading);
            _customers = await _customerService.GetCustomersAsync(_searchText);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[CustomerListForm] LoadDataAsync failed: {ex}");
            SetState(CustomerState.Error);
            RtlMessageBox.Show("حدث خطأ أثناء تحميل العملاء", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyFilter()
    {
        _filteredCustomers = string.IsNullOrEmpty(_searchText)
            ? _customers.ToList()
            : _customers.Where(c => c.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) || (c.Phone?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

        PopulateGrid();
        _lblCount.Text = $"العملاء: {_filteredCustomers.Count}";
        SetState(_filteredCustomers.Count > 0 ? CustomerState.Loaded : CustomerState.Empty);
    }

    private void PopulateGrid()
    {
        var table = new DataTable();
        table.Columns.Add("__Id", typeof(Guid));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Phone", typeof(string));
        table.Columns.Add("Email", typeof(string));
        table.Columns.Add("Balance", typeof(string));
        table.Columns.Add("RawBalance", typeof(decimal));

        foreach (var c in _filteredCustomers)
        {
            var row = table.NewRow();
            row["__Id"] = c.Id;
            row["Name"] = c.Name;
            row["Phone"] = c.Phone ?? "";
            row["Email"] = c.Email ?? "";
            row["Balance"] = $"{DesignTokens.FormatJOD(c.Balance)} JOD";
            row["RawBalance"] = c.Balance;
            table.Rows.Add(row);
        }
        _customersGrid.SetDataSource(table);
    }

    private async Task ShowCustomerActionsMenu(CustomerDto customer)
    {
        var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };
        var editItem = new ToolStripMenuItem("✏️ تعديل");
        editItem.Click += (s, e) => ShowCustomerDialog(customer);
        menu.Items.Add(editItem);

        var historyItem = new ToolStripMenuItem("📋 سجل الطلبات");
        historyItem.Click += (s, e) => ShowOrderHistory(customer);
        menu.Items.Add(historyItem);

        menu.Items.Add(new ToolStripSeparator());

        var deleteItem = new ToolStripMenuItem("🗑 حذف");
        deleteItem.Click += (s, e) => _ = DeleteCustomerAsync(customer);
        menu.Items.Add(deleteItem);

        menu.Show(this, PointToClient(MousePosition));
    }

    private void ShowCustomerDialog(CustomerDto? existing)
    {
        var isEdit = existing != null;
        var dialog = new RtlDialog(isEdit ? "تعديل بيانات العميل" : "إضافة عميل جديد", 480, 420);
        var layout = new TableLayoutPanel { ColumnCount = 2, RowCount = 8, Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, BackColor = DesignTokens.Colors.Surface, Padding = new Padding(DesignTokens.Spacing.Standard) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 8; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

        layout.Controls.Add(CreateDlgLabel("الاسم *:"), 0, 0);
        var txtName = new RtlTextBox { Text = existing?.Name ?? "", Dock = DockStyle.Fill, IsRequired = true };
        layout.Controls.Add(txtName, 1, 0);

        layout.Controls.Add(CreateDlgLabel("الهاتف:"), 0, 1);
        layout.Controls.Add(new RtlTextBox { Text = existing?.Phone ?? "", Dock = DockStyle.Fill }, 1, 1);

        layout.Controls.Add(CreateDlgLabel("البريد الإلكتروني:"), 0, 2);
        layout.Controls.Add(new RtlTextBox { Text = existing?.Email ?? "", Dock = DockStyle.Fill }, 1, 2);

        layout.Controls.Add(CreateDlgLabel("العنوان:"), 0, 3);
        layout.Controls.Add(new RtlTextBox { Text = existing?.Address ?? "", Dock = DockStyle.Fill }, 1, 3);

        layout.Controls.Add(CreateDlgLabel("الرصيد الحالي:"), 0, 5);
        var lblBalance = new Label { Text = existing != null ? $"{DesignTokens.FormatJOD(existing.Balance)} JOD" : "0.000 JOD", Font = DesignTokens.Typography.BodyBold, ForeColor = existing?.Balance < 0 ? DesignTokens.Colors.Error : DesignTokens.Colors.Success, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        layout.Controls.Add(lblBalance, 1, 5);

        dialog.ContentArea.Controls.Add(layout);
        dialog.AddAction(isEdit ? "تحديث" : "إضافة", async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txtName.Text)) { RtlMessageBox.Show("يرجى إدخال اسم العميل", "حقل مطلوب", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            try
            {
                if (isEdit && existing != null)
                {
                    await _customerService.UpdateCustomerAsync(existing.Id, txtName.Text.Trim(), null, null, null, null);
                }
                else
                {
                    await _customerService.CreateCustomerAsync(txtName.Text.Trim(), null, null);
                }
                await LoadDataAsync();
                dialog.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[CustomerListForm] CreateCustomer failed: {ex}");
                RtlMessageBox.Show("حدث خطأ أثناء إنشاء العميل", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });
        dialog.AddAction("إلغاء", (s, e) => dialog.Close(), false);
        dialog.ShowDialog(this.FindForm());
    }

    private async void ShowOrderHistory(CustomerDto customer)
    {
        var dialog = new RtlDialog($"سجل طلبات العميل: {customer.Name}", 700, 450);
        var grid = new RtlGridControl();
        grid.AddTextColumn("InvoiceNumber", "رقم الفاتورة", 120, HorzAlignment.Center);
        grid.AddTextColumn("Date", "التاريخ", 150);
        grid.AddTextColumn("Amount", "المبلغ", 130, HorzAlignment.Far);
        grid.AddTextColumn("Status", "الحالة", 100, HorzAlignment.Center);

        var table = new DataTable();
        table.Columns.Add("InvoiceNumber", typeof(string));
        table.Columns.Add("Date", typeof(string));
        table.Columns.Add("Amount", typeof(string));
        table.Columns.Add("Status", typeof(string));

        try
        {
            var orders = await _customerService.GetCustomerOrderHistoryAsync(customer.Id);
            foreach (var order in orders)
            {
                var r = table.NewRow();
                r["InvoiceNumber"] = order.InvoiceNumber;
                r["Date"] = order.CreatedAt.ToString("yyyy/MM/dd HH:mm");
                r["Amount"] = $"{DesignTokens.FormatJOD(order.TotalAmount)} JOD";
                r["Status"] = order.Status;
                table.Rows.Add(r);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[CustomerListForm] ShowOrderHistory failed: {ex}");
            var r = table.NewRow();
            r["InvoiceNumber"] = "---";
            r["Date"] = "---";
            r["Amount"] = "---";
            r["Status"] = "---";
            table.Rows.Add(r);
        }

        grid.SetDataSource(table);

        grid.GridViewCore.RowCellStyle += (s, e) =>
        {
            if (e.Column.FieldName == "Status")
            {
                var status = grid.GridViewCore.GetRowCellValue(e.RowHandle, "Status")?.ToString();
                e.Appearance.ForeColor = status switch { "مكتمل" => DesignTokens.Colors.Success, "ملغي" => DesignTokens.Colors.Error, "معلق" => DesignTokens.Colors.Warning, _ => DesignTokens.Colors.TextPrimary };
                e.Appearance.Options.UseForeColor = true;
            }
        };

        dialog.ContentArea.Controls.Add(grid);
        dialog.AddAction("إغلاق", (s, e) => dialog.Close(), false);
        dialog.ShowDialog(this.FindForm());
    }

    private async Task DeleteCustomerAsync(CustomerDto customer)
    {
        if (RtlDialog.ShowDestructiveConfirm("حذف عميل", $"هل أنت متأكد من حذف العميل \"{customer.Name}\"?\n\nسيتم حذف جميع سجلاته نهائياً.") == DialogResult.OK)
        {
            try
            {
                await _customerService.DeleteCustomerAsync(customer.Id);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[CustomerListForm] DeleteCustomerAsync failed: {ex}");
                RtlMessageBox.Show("حدث خطأ أثناء حذف العميل", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private Label CreateDlgLabel(string text) => new Label { Text = text, Font = DesignTokens.Typography.Body, ForeColor = DesignTokens.Colors.TextPrimary, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, Margin = new Padding(0, DesignTokens.Spacing.Micro, 0, 0) };
}
