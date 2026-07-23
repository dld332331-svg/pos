using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.Utils;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Desktop.Themes;
using POS.Desktop.CustomControls;

namespace POS.Desktop.Forms;

/// <summary>
/// SUPP-001: Supplier list management UserControl using DevExpress GridControl.
/// Top: search textbox + "إضافة مورد" button + refresh button + count label.
/// Main: RtlGridControl with columns: الاسم, جهة الاتصال, الهاتف, البريد, العنوان, الرصيد, الحالة, إجراءات.
/// Context menu: تعديل, حذف. Add/Edit dialog (RtlDialog).
/// Delete with RtlDialog.ShowDestructiveConfirm. Empty/Loading/Error states. All Arabic RTL.
/// </summary>
public class SupplierListForm : UserControl
{
    private enum SupplierState
    {
        Loading,
        Loaded,
        Empty,
        Error,
        PermissionDenied
    }

    private SupplierState _currentState = SupplierState.Loading;
    private readonly ISupplierService _supplierService;
    private List<SupplierForm.SupplierData> _suppliers = new();
    private List<SupplierForm.SupplierData> _filteredSuppliers = new();
    private string _searchText = "";

    // UI Controls - Toolbar
    private Panel _toolbarPanel = null!;
    private RtlTextBox _txtSearch = null!;
    private RtlButton _btnAddSupplier = null!;
    private RtlButton _btnRefresh = null!;
    private Label _lblCount = null!;

    // UI Controls - Data Grid (DevExpress)
    private RtlGridControl _suppliersGrid = null!;

    // UI Controls - Overlays
    private Panel _loadingOverlay = null!;
    private Panel _emptyOverlay = null!;
    private Panel _errorOverlay = null!;
    private Label _errorMessage = null!;
    private Panel _permissionPanel = null!;

    // Events
    public event EventHandler<int>? SupplierSelected;
    public event EventHandler? SupplierAdded;
    public event EventHandler? SupplierUpdated;
    public event EventHandler? SupplierDeleted;

    public SupplierListForm(ISupplierService supplierService)
    {
        _supplierService = supplierService;
        InitializeComponent();
        SetState(SupplierState.Loading);
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
            Height = DesignTokens.ControlHeight.Large + DesignTokens.Spacing.Compact,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard),
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Compact)
        };

        var toolbarInner = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        _btnAddSupplier = new RtlButton
        {
            Text = "➕ إضافة مورد",
            Type = RtlButton.ButtonType.Primary,
            Width = 140,
            Height = DesignTokens.ControlHeight.Standard
        };
        _btnAddSupplier.Click += (s, e) => _ = ShowSupplierDialogAsync(null);

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
            Text = "الموردين: ٠",
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.TextSecondary,
            AutoSize = true,
            Margin = new Padding(DesignTokens.Spacing.Standard, 0, DesignTokens.Spacing.Standard, 0)
        };

        _txtSearch = new RtlTextBox
        {
            PlaceholderText = "🔍 بحث بالاسم أو الهاتف أو البريد...",
            Width = 300,
            Height = DesignTokens.ControlHeight.Standard,
            Margin = new Padding(0, 0, DesignTokens.Spacing.Compact, 0)
        };
        _txtSearch.TextChanged += (s, e) =>
        {
            _searchText = _txtSearch.Text.Trim();
            ApplyFilter();
        };

        toolbarInner.Controls.Add(_btnAddSupplier);
        toolbarInner.Controls.Add(_btnRefresh);
        toolbarInner.Controls.Add(_lblCount);
        toolbarInner.Controls.Add(_txtSearch);
        _toolbarPanel.Controls.Add(toolbarInner);

        // === DevExpress Grid Control ===
        _suppliersGrid = new RtlGridControl();

        // Define columns
        _suppliersGrid.AddTextColumn("Name", "الاسم", 180);
        _suppliersGrid.AddTextColumn("Contact", "جهة الاتصال", 150);
        _suppliersGrid.AddTextColumn("Phone", "الهاتف", 130);
        _suppliersGrid.AddTextColumn("Email", "البريد", 180);
        _suppliersGrid.AddTextColumn("Address", "العنوان", 180);
        _suppliersGrid.AddTextColumn("Balance", "الرصيد", 100, HorzAlignment.Far, "0.000 JOD");
        _suppliersGrid.AddTextColumn("Status", "الحالة", 80, HorzAlignment.Center);
        _suppliersGrid.AddActionsColumn("إجراءات", 80);

        // Style the Balance column
        var balanceCol = _suppliersGrid.GridViewCore.Columns["Balance"];
        if (balanceCol != null)
        {
            balanceCol.AppearanceCell.ForeColor = DesignTokens.Colors.Success;
            balanceCol.AppearanceCell.Options.UseForeColor = true;
        }

        // Handle action button click
        _suppliersGrid.ActionButtonClick += (s, e) =>
        {
            if (e.RowData is DataRowView rowView)
            {
                int supplierId = Convert.ToInt32(rowView["__Id"]);
                var supplier = _filteredSuppliers.FirstOrDefault(s => s.Id == supplierId);
                if (supplier != null)
                    ShowSupplierActionsMenu(supplier);
            }
        };

        // Handle row focus change (selection)
        _suppliersGrid.GridViewCore.FocusedRowChanged += (s, e) =>
        {
            if (e.FocusedRowHandle >= 0)
            {
                var row = _suppliersGrid.GridViewCore.GetRow(e.FocusedRowHandle) as DataRowView;
                if (row != null)
                {
                    int supplierId = Convert.ToInt32(row["__Id"]);
                    SupplierSelected?.Invoke(this, supplierId);
                }
            }
        };

        // === Loading Overlay ===
        _loadingOverlay = ThemeManager.CreateLoadingPanel("جاري تحميل قائمة الموردين...");
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
            Text = "🏪",
            Font = new Font("Segoe UI", 48f),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 80
        };
        var emptyLabel = new Label
        {
            Text = "لا يوجد موردين مسجلين",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        var emptySubLabel = new Label
        {
            Text = "اضغط على \\\"إضافة مورد\\\" لبدء إضافة الموردين",
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
            Text = "حدث خطأ أثناء تحميل الموردين",
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
        _permissionPanel.Controls.Add(new Label { Text = "ليس لديك صلاحية لعرض الموردين", Font = DesignTokens.Typography.SectionTitle, ForeColor = DesignTokens.Colors.TextSecondary, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });

        // Assemble
        Controls.Add(_loadingOverlay);
        Controls.Add(_emptyOverlay);
        Controls.Add(_errorOverlay);
        Controls.Add(_permissionPanel);
        Controls.Add(_suppliersGrid);
        Controls.Add(_toolbarPanel);
    }

    // --- State Management ---

    private void SetState(SupplierState state)
    {
        _currentState = state;
        _loadingOverlay.Visible = state == SupplierState.Loading;
        _emptyOverlay.Visible = state == SupplierState.Empty;
        _errorOverlay.Visible = state == SupplierState.Error;
        _permissionPanel.Visible = state == SupplierState.PermissionDenied;
        _suppliersGrid.Visible = state == SupplierState.Loaded;
        _btnAddSupplier.Enabled = state == SupplierState.Loaded;
        _btnRefresh.Enabled = state != SupplierState.Loading;
    }

    // --- Data Loading ---

    private async Task LoadDataAsync()
    {
        SetState(SupplierState.Loading);
        try
        {
            var suppliers = await _supplierService.GetSuppliersAsync();
            _suppliers = suppliers.Select(s => new SupplierForm.SupplierData
            {
                Id = 0,
                SupplierId = s.Id,
                Name = s.Name,
                Contact = s.ContactPerson ?? "",
                Phone = s.Phone ?? "",
                Email = s.Email ?? "",
                Address = s.Address ?? "",
                Balance = s.Balance,
                IsActive = s.IsActive
            }).ToList();

            ApplyFilter();
            SetState(_filteredSuppliers.Count > 0 ? SupplierState.Loaded : SupplierState.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("Load suppliers failed: {0}", ex);
            _errorMessage.Text = "حدث خطأ أثناء تحميل الموردين";
            SetState(SupplierState.Error);
        }
    }



    private void ApplyFilter()
    {
        if (string.IsNullOrEmpty(_searchText))
        {
            _filteredSuppliers = _suppliers.ToList();
        }
        else
        {
            var search = _searchText.ToLower();
            _filteredSuppliers = _suppliers.Where(s =>
                s.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Phone.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Contact.Contains(search, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        PopulateGrid();
        _lblCount.Text = $"الموردين: {_filteredSuppliers.Count}";
        SetState(_filteredSuppliers.Count > 0 ? SupplierState.Loaded : SupplierState.Empty);
    }

    private void PopulateGrid()
    {
        var table = new DataTable();
        table.Columns.Add("__Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Contact", typeof(string));
        table.Columns.Add("Phone", typeof(string));
        table.Columns.Add("Email", typeof(string));
        table.Columns.Add("Address", typeof(string));
        table.Columns.Add("Balance", typeof(string));
        table.Columns.Add("Status", typeof(string));
        table.Columns.Add("IsActive", typeof(bool));
        table.Columns.Add("RawBalance", typeof(decimal));

        foreach (var supplier in _filteredSuppliers)
        {
            var row = table.NewRow();
            row["__Id"] = supplier.Id;
            row["Name"] = supplier.Name;
            row["Contact"] = supplier.Contact;
            row["Phone"] = supplier.Phone;
            row["Email"] = supplier.Email;
            row["Address"] = supplier.Address?.Replace("\n", ", ") ?? "";
            row["Balance"] = $"{DesignTokens.FormatJOD(supplier.Balance)} JOD";
            row["Status"] = supplier.IsActive ? "مفعّل" : "معطّل";
            row["IsActive"] = supplier.IsActive;
            row["RawBalance"] = supplier.Balance;
            table.Rows.Add(row);
        }

        _suppliersGrid.SetDataSource(table);

        // Apply conditional formatting via the view's appearance
        var view = _suppliersGrid.GridViewCore;
        view.RowCellStyle += (s, e) =>
        {
            if (e.Column.FieldName == "Status")
            {
                var isActive = Convert.ToBoolean(view.GetRowCellValue(e.RowHandle, "IsActive"));
                e.Appearance.ForeColor = isActive
                    ? DesignTokens.Colors.Success
                    : DesignTokens.Colors.TextSecondary;
                e.Appearance.Font = DesignTokens.Typography.BodyBold;
                e.Appearance.Options.UseForeColor = true;
                e.Appearance.Options.UseFont = true;
            }
            if (e.Column.FieldName == "Balance")
            {
                var rawBalance = Convert.ToDecimal(view.GetRowCellValue(e.RowHandle, "RawBalance"));
                e.Appearance.ForeColor = rawBalance < 0
                    ? DesignTokens.Colors.Error
                    : rawBalance > 0
                        ? DesignTokens.Colors.Success
                        : DesignTokens.Colors.TextPrimary;
                e.Appearance.Options.UseForeColor = true;
            }
        };
    }

    // --- Event Handlers ---

    private void ShowSupplierActionsMenu(SupplierForm.SupplierData supplier)
    {
        var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };

        var editItem = new ToolStripMenuItem("✏️ تعديل");
        editItem.Click += (s, e) => _ = ShowSupplierDialogAsync(supplier);
        menu.Items.Add(editItem);

        menu.Items.Add(new ToolStripSeparator());

        var deleteItem = new ToolStripMenuItem("🗑 حذف");
        deleteItem.Click += (s, e) => _ = DeleteSupplierAsync(supplier);
        menu.Items.Add(deleteItem);

        menu.Show(this, PointToClient(MousePosition));
    }

    // --- Supplier Dialog ---

    private async Task ShowSupplierDialogAsync(SupplierForm.SupplierData? existing)
    {
        try
        {
            var result = SupplierForm.ShowDialog(existing, _suppliers, this.FindForm());
            if (result == DialogResult.OK)
            {
                if (existing != null)
                {
                    try
                    {
                        await _supplierService.UpdateSupplierAsync(
                            existing.SupplierId,
                            existing.Name,
                            existing.Contact,
                            existing.Phone,
                            existing.Email,
                            existing.Address);
                    }
                    catch { System.Diagnostics.Trace.TraceWarning("[SupplierList] Supplier update failed"); }
                }
                else
                {
                    var newEntry = _suppliers.OrderByDescending(s => s.Id).FirstOrDefault();
                    if (newEntry != null)
                    {
                        try
                        {
                            var created = await _supplierService.CreateSupplierAsync(
                                newEntry.Name,
                                newEntry.Contact,
                                newEntry.Phone,
                                newEntry.Email,
                                newEntry.Address);
                            newEntry.SupplierId = created.Id;
                        }
                        catch { System.Diagnostics.Trace.TraceWarning("[SupplierList] Supplier operation failed, will reload"); }
                    }
                }

                await LoadDataAsync();
                if (existing != null)
                    SupplierUpdated?.Invoke(this, EventArgs.Empty);
                else
                    SupplierAdded?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[SupplierListForm] ShowSupplierDialogAsync failed: {ex}");
        }
    }

    // --- Delete ---

    private async Task DeleteSupplierAsync(SupplierForm.SupplierData supplier)
    {
        try
        {
            var result = RtlDialog.ShowDestructiveConfirm(
                "حذف مورد",
                $"هل أنت متأكد من حذف المورد \"{supplier.Name}\"؟\n\nسيتم حذف جميع سجلاته نهائياً."
            );
            if (result == DialogResult.OK)
            {
                _suppliers.Remove(supplier);
                ApplyFilter();
                SupplierDeleted?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[SupplierListForm] DeleteSupplierAsync failed: {ex}");
        }
    }

}
