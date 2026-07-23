using System.Drawing;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Desktop.Themes;
using POS.Desktop.CustomControls;

namespace POS.Desktop.Forms;

/// <summary>
/// DEV-001: Printer management UserControl.
/// TabControl with 2 tabs: Printers, Kitchen Stations.
/// Printers tab: Add button, DataGridView with printer list, Add/Edit dialog with test print.
/// Kitchen Stations tab: stations list with name, printer assignment, active status.
/// </summary>
public class PrinterManagementForm : UserControl
{
    private enum PrinterMgmtState
    {
        Loading,
        Loaded,
        Empty,
        Error,
        PermissionDenied
    }

    private PrinterMgmtState _currentState = PrinterMgmtState.Loading;
    private List<PrinterEntry> _printers = new();
    private List<KitchenStationEntry> _kitchenStations = new();
    private readonly IPrinterService _printerService;
    private readonly IPrinterManagementService _printerManagementService;

    // Root Controls
    private TabControl _tabControl;
    private TabPage _tabPrinters;
    private TabPage _tabKitchenStations;

    // Printers Tab
    private Panel _printerToolbar;
    private RtlButton _btnAddPrinter;
    private RtlDataGridView _printersGrid;

    // Kitchen Stations Tab
    private Panel _stationToolbar;
    private RtlButton _btnAddStation;
    private RtlDataGridView _stationsGrid;

    // Overlays
    private Panel _loadingOverlay;
    private Panel _permissionPanel = null!;

    public PrinterManagementForm(IPrinterService printerService, IPrinterManagementService printerManagementService)
    {
        _printerService = printerService;
        _printerManagementService = printerManagementService;
        InitializeComponent();
        SetState(PrinterMgmtState.Loading);
        _ = LoadDataAsync();
    }

    private void InitializeComponent()
    {
        RightToLeft = RightToLeft.Yes;
        BackColor = DesignTokens.Colors.Background;
        Font = DesignTokens.Typography.Body;
        Dock = DockStyle.Fill;

        // === TabControl ===
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes,
            Font = DesignTokens.Typography.BodyBold,
            Padding = new Point(DesignTokens.Spacing.Standard, DesignTokens.Spacing.Compact)
        };

        // --- Printers Tab ---
        _tabPrinters = new TabPage
        {
            Text = "🖨 الطابعات",
            RightToLeft = RightToLeft.Yes,
            BackColor = DesignTokens.Colors.Background,
            Padding = new Padding(0)
        };

        _printerToolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = DesignTokens.ControlHeight.Large + DesignTokens.Spacing.Compact,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard),
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Compact)
        };

        var printerToolbarInner = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        _btnAddPrinter = new RtlButton
        {
            Text = "➕ إضافة طابعة",
            Type = RtlButton.ButtonType.Primary,
            Width = 160,
            Height = DesignTokens.ControlHeight.Standard
        };
        _btnAddPrinter.Click += (s, e) => ShowPrinterDialog(null);

        printerToolbarInner.Controls.Add(_btnAddPrinter);
        _printerToolbar.Controls.Add(printerToolbarInner);

        _printersGrid = new RtlDataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true
        };

        _printersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الاسم", Name = "Name", FillWeight = 18 });
        _printersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "النوع", Name = "Type", FillWeight = 12 });
        _printersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الاتصال", Name = "Connection", FillWeight = 12 });
        _printersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "العنوان/المنفذ", Name = "Address", FillWeight = 18 });
        _printersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "عرض الورق", Name = "PaperWidth", FillWeight = 10 });
        _printersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الدور", Name = "Role", FillWeight = 12 });
        _printersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الحالة", Name = "Status", FillWeight = 8 });
        _printersGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "إجراءات", Name = "Actions", FillWeight = 10, Text = "إجراءات", UseColumnTextForButtonValue = true });

        _printersGrid.CellClick += PrintersGrid_CellClick;
        _printersGrid.CellFormatting += PrintersGrid_CellFormatting;

        _tabPrinters.Controls.Add(_printersGrid);
        _tabPrinters.Controls.Add(_printerToolbar);

        // --- Kitchen Stations Tab ---
        _tabKitchenStations = new TabPage
        {
            Text = "🍳 محطات المطبخ",
            RightToLeft = RightToLeft.Yes,
            BackColor = DesignTokens.Colors.Background,
            Padding = new Padding(0)
        };

        _stationToolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = DesignTokens.ControlHeight.Large + DesignTokens.Spacing.Compact,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard),
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Compact)
        };

        var stationToolbarInner = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        _btnAddStation = new RtlButton
        {
            Text = "➕ إضافة محطة",
            Type = RtlButton.ButtonType.Primary,
            Width = 150,
            Height = DesignTokens.ControlHeight.Standard
        };
        _btnAddStation.Click += (s, e) => ShowStationDialog(null);

        stationToolbarInner.Controls.Add(_btnAddStation);
        _stationToolbar.Controls.Add(stationToolbarInner);

        _stationsGrid = new RtlDataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true
        };

        _stationsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "اسم المحطة", Name = "StationName", FillWeight = 25 });
        _stationsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الطابعة المخصصة", Name = "Printer", FillWeight = 30 });
        _stationsGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "مفعّلة", Name = "Active", FillWeight = 15 });
        _stationsGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "إجراءات", Name = "Actions", FillWeight = 15, Text = "إجراءات", UseColumnTextForButtonValue = true });

        _stationsGrid.CellClick += StationsGrid_CellClick;
        _stationsGrid.CellFormatting += StationsGrid_CellFormatting;

        _tabKitchenStations.Controls.Add(_stationsGrid);
        _tabKitchenStations.Controls.Add(_stationToolbar);

        // Add tabs
        _tabControl.TabPages.AddRange(new TabPage[] { _tabPrinters, _tabKitchenStations });

        // === Loading Overlay ===
        _loadingOverlay = ThemeManager.CreateLoadingPanel("جاري تحميل بيانات الطابعات...");
        _loadingOverlay.Visible = false;

        _permissionPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _permissionPanel.Controls.Add(new Label { Text = "ليس لديك صلاحية لإدارة الطابعات", Font = DesignTokens.Typography.SectionTitle, ForeColor = DesignTokens.Colors.TextSecondary, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });

        // Assemble
        Controls.Add(_loadingOverlay);
        Controls.Add(_permissionPanel);
        Controls.Add(_tabControl);
    }

    // --- State Management ---

    private void SetState(PrinterMgmtState state)
    {
        _currentState = state;
        _loadingOverlay.Visible = state == PrinterMgmtState.Loading;
        _permissionPanel.Visible = state == PrinterMgmtState.PermissionDenied;
        var showGrid = state == PrinterMgmtState.Loaded || state == PrinterMgmtState.Empty;
        _printersGrid.Visible = showGrid;
        _stationsGrid.Visible = showGrid;
        _btnAddPrinter.Enabled = state == PrinterMgmtState.Loaded;
        _btnAddStation.Enabled = state == PrinterMgmtState.Loaded;
    }

    // --- Data Loading ---

    private async Task LoadDataAsync()
    {
        SetState(PrinterMgmtState.Loading);
        try
        {
            var printers = await _printerManagementService.GetPrintersAsync();
            _printers = printers.Select(p => new PrinterEntry
            {
                PrinterId = p.Id,
                Name = p.Name,
                Type = p.PrinterType,
                Connection = p.Connection,
                Address = p.Connection == "Network" ? (p.IpAddress ?? "") : (p.Port ?? ""),
                PaperWidth = p.PaperWidth + "mm",
                Role = p.AssignedRole,
                Enabled = p.IsActive
            }).ToList();

            var stations = await _printerManagementService.GetKitchenStationsAsync();
            _kitchenStations = stations.Select(s => new KitchenStationEntry
            {
                StationId = s.Id,
                StationName = s.Name,
                PrinterName = s.PrinterName ?? "",
                Active = s.IsActive
            }).ToList();

            PopulatePrinterGrid();
            PopulateStationGrid();
            SetState(PrinterMgmtState.Loaded);
        }
        catch (Exception)
        {
            _printers.Clear();
            _kitchenStations.Clear();
            PopulatePrinterGrid();
            PopulateStationGrid();
            SetState(PrinterMgmtState.Empty);
        }
    }



    private void PopulatePrinterGrid()
    {
        _printersGrid.Rows.Clear();
        foreach (var printer in _printers)
        {
            var typeLabel = printer.Type switch
            {
                "Thermal" => "حرارية",
                "DotMatrix" => "نقطية",
                _ => printer.Type
            };
            var connLabel = printer.Connection switch
            {
                "USB" => "USB",
                "Network" => "شبكة",
                "Serial" => "تسلسلي",
                _ => printer.Connection
            };
            var roleLabel = printer.Role switch
            {
                "Receipt" => "إيصالات",
                "Kitchen" => "مطبخ",
                "Beverage" => "مشروبات",
                "Department" => "قسم",
                _ => printer.Role
            };

            _printersGrid.Rows.Add(
                printer.Name, typeLabel, connLabel, printer.Address,
                printer.PaperWidth, roleLabel,
                printer.Enabled ? "مفعّلة" : "معطلة",
                "إجراءات"
            );
            _printersGrid.Rows[_printersGrid.Rows.Count - 1].Tag = printer;
        }
        _printersGrid.ShowEmptyMessage("لا توجد طابعات مسجلة");
    }

    private void PopulateStationGrid()
    {
        _stationsGrid.Rows.Clear();
        foreach (var station in _kitchenStations)
        {
            _stationsGrid.Rows.Add(
                station.StationName,
                string.IsNullOrEmpty(station.PrinterName) ? "غير محددة" : station.PrinterName,
                station.Active,
                "إجراءات"
            );
            _stationsGrid.Rows[_stationsGrid.Rows.Count - 1].Tag = station;
        }
        _stationsGrid.ShowEmptyMessage("لا توجد محطات مطبخ");
    }

    // --- Cell Formatting ---

    private void PrintersGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_printersGrid.Columns[e.ColumnIndex].Name == "Status")
        {
            var text = e.Value?.ToString() ?? "";
            e.CellStyle.ForeColor = text == "مفعّلة" ? DesignTokens.Colors.Success : DesignTokens.Colors.Disabled;
        }
    }

    private void StationsGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_printersGrid.Columns[e.ColumnIndex].Name == "Printer")
        {
            if (e.Value?.ToString() == "غير محددة")
                e.CellStyle.ForeColor = DesignTokens.Colors.Warning;
        }
    }

    // --- Event Handlers ---

    private void PrintersGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_printersGrid.Columns[e.ColumnIndex].Name != "Actions") return;

        var printer = _printersGrid.Rows[e.RowIndex].Tag as PrinterEntry;
        if (printer == null) return;

        var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };
        var editItem = new ToolStripMenuItem("✏️ تعديل");
        editItem.Click += (s, e) => ShowPrinterDialog(printer);
        menu.Items.Add(editItem);

        var testConnItem = new ToolStripMenuItem("🔌 اختبار الاتصال");
        testConnItem.Click += (s, e) => _ = TestPrinterConnectionQuickAsync(printer);
        menu.Items.Add(testConnItem);

        var testPrintItem = new ToolStripMenuItem("🖨 اختبار الطباعة");
        testPrintItem.Click += (s, e) => TestPrinter(printer);
        menu.Items.Add(testPrintItem);

        var deleteItem = new ToolStripMenuItem("🗑 حذف");
        deleteItem.Click += (s, e) => DeletePrinter(printer);
        menu.Items.Add(deleteItem);

        var cellRect = _printersGrid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
        menu.Show(_printersGrid, cellRect.Left, cellRect.Bottom);
    }

    private void StationsGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_stationsGrid.Columns[e.ColumnIndex].Name != "Actions") return;

        var station = _stationsGrid.Rows[e.RowIndex].Tag as KitchenStationEntry;
        if (station == null) return;

        var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };
        var editItem = new ToolStripMenuItem("✏️ تعديل");
        editItem.Click += (s, e) => ShowStationDialog(station);
        menu.Items.Add(editItem);

        var deleteItem = new ToolStripMenuItem("🗑 حذف");
        deleteItem.Click += (s, e) => DeleteStation(station);
        menu.Items.Add(deleteItem);

        var cellRect = _stationsGrid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
        menu.Show(_stationsGrid, cellRect.Left, cellRect.Bottom);
    }

    // --- Printer Dialog ---

    private RtlTextBox txtIpAddress = null!;
    private Label? _lblConnectionStatus;

    private void ShowPrinterDialog(PrinterEntry? existing)
    {
        var isEdit = existing != null;
        var dialog = new RtlDialog(isEdit ? "تعديل طابعة" : "إضافة طابعة جديدة", 560, 600);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 12,
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 12; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        // Row 0: Name
        layout.Controls.Add(CreateDlgLabel("الاسم:"), 0, 0);
        var txtName = new RtlTextBox { Text = existing?.Name ?? "", Dock = DockStyle.Fill, IsRequired = true };
        layout.Controls.Add(txtName, 1, 0);

        // Row 1: Type
        layout.Controls.Add(CreateDlgLabel("النوع:"), 0, 1);
        var cmbType = new RtlComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        cmbType.Items.AddRange(new object[] { "Thermal", "DotMatrix" });
        cmbType.SelectedItem = existing?.Type ?? "Thermal";
        layout.Controls.Add(cmbType, 1, 1);

        // Row 2: Connection
        layout.Controls.Add(CreateDlgLabel("الاتصال:"), 0, 2);
        var cmbConnection = new RtlComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        cmbConnection.Items.AddRange(new object[] { "USB", "Network", "Serial" });
        cmbConnection.SelectedItem = existing?.Connection ?? "USB";
        layout.Controls.Add(cmbConnection, 1, 2);

        // Row 3: IP Address
        layout.Controls.Add(CreateDlgLabel("عنوان IP:"), 0, 3);
        txtIpAddress = new RtlTextBox { Text = existing?.Connection == "Network" ? existing.Address : "192.168.1.", Dock = DockStyle.Fill, Enabled = existing?.Connection == "Network" };
        layout.Controls.Add(txtIpAddress, 1, 3);

        // Row 4: Port / ConnectionString
        layout.Controls.Add(CreateDlgLabel("المنفذ/COM:"), 0, 4);
        var txtPort = new RtlTextBox { Text = existing?.Connection != "Network" ? existing?.Address ?? "" : "COM1", Dock = DockStyle.Fill, Enabled = existing?.Connection != "Network" };
        layout.Controls.Add(txtPort, 1, 4);

        cmbConnection.SelectedIndexChanged += (s, e) =>
        {
            var isNetwork = cmbConnection.SelectedItem?.ToString() == "Network";
            txtPort.Enabled = !isNetwork;
            txtIpAddress.Enabled = isNetwork;
        };

        // Row 5: BaudRate (only for Serial/Virtual COM)
        layout.Controls.Add(CreateDlgLabel("معدل الباود:"), 0, 5);
        var cmbBaudRate = new RtlComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        cmbBaudRate.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200" });
        cmbBaudRate.SelectedItem = existing?.BaudRate.ToString() ?? "9600";
        layout.Controls.Add(cmbBaudRate, 1, 5);

        // Row 6: Paper Width
        layout.Controls.Add(CreateDlgLabel("عرض الورق:"), 0, 6);
        var cmbPaperWidth = new RtlComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        cmbPaperWidth.Items.AddRange(new object[] { "58mm", "80mm" });
        cmbPaperWidth.SelectedItem = existing?.PaperWidth ?? "80mm";
        layout.Controls.Add(cmbPaperWidth, 1, 6);

        // Row 7: Role
        layout.Controls.Add(CreateDlgLabel("الدور:"), 0, 7);
        var cmbRole = new RtlComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        cmbRole.Items.AddRange(new object[] { "Receipt", "Kitchen", "Beverage", "Department" });
        cmbRole.SelectedItem = existing?.Role ?? "Receipt";
        layout.Controls.Add(cmbRole, 1, 7);

        // Row 8: Enabled
        layout.Controls.Add(CreateDlgLabel("مفعّلة:"), 0, 8);
        var chkEnabled = new CheckBox { Text = "تفعيل الطابعة", RightToLeft = RightToLeft.Yes, Font = DesignTokens.Typography.Body, Checked = existing?.Enabled ?? true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        layout.Controls.Add(chkEnabled, 1, 8);

        // Row 9: Connection test button + status indicator (span 2 columns)
        var testPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Height = DesignTokens.ControlHeight.Standard
        };

        var btnTestConnection = new RtlButton
        {
            Text = "🔌 اختبار الاتصال",
            Type = RtlButton.ButtonType.Secondary,
            Width = 160,
            Height = DesignTokens.ControlHeight.Standard
        };

        _lblConnectionStatus = new Label
        {
            Text = "لم يتم الاختبار",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextSecondary,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = true,
            Margin = new Padding(DesignTokens.Spacing.Standard, 0, 0, 0)
        };

        btnTestConnection.Click += (s, e) =>
        {
            btnTestConnection.IsLoading = true;
            _lblConnectionStatus.Text = "جاري الاختبار...";
            _lblConnectionStatus.ForeColor = DesignTokens.Colors.Info;

            // Run the status check on a background thread to avoid blocking the UI
            _ = Task.Run(async () =>
            {
                try
                {
                    var status = await TestPrinterConnectionAsync(
                        txtName.Text,
                        cmbType.SelectedItem?.ToString() ?? "Thermal",
                        cmbConnection.SelectedItem?.ToString() ?? "USB",
                        cmbConnection.SelectedItem?.ToString() == "Network" ? txtIpAddress.Text : txtPort.Text,
                        int.TryParse(cmbBaudRate.SelectedItem?.ToString(), out var br) ? br : 9600);

                    // Update UI on the main thread
                    this.FindForm()?.Invoke(() =>
                    {
                        if (btnTestConnection.IsDisposed) return;
                        btnTestConnection.IsLoading = false;
                        UpdateConnectionStatusLabel(status);
                    });
                }
                catch (Exception ex)
                {
                    this.FindForm()?.Invoke(() =>
                    {
                        if (btnTestConnection.IsDisposed) return;
                        btnTestConnection.IsLoading = false;
                        _lblConnectionStatus!.Text = "❌ خطأ في الاتصال بالطابعة";
                        _lblConnectionStatus!.ForeColor = DesignTokens.Colors.Error;
                        System.Diagnostics.Trace.TraceError($"[PrinterManagementForm] TestConnection failed: {ex}");
                    });
                }
            });
        };

        testPanel.Controls.Add(btnTestConnection);
        testPanel.Controls.Add(_lblConnectionStatus);
        layout.Controls.Add(testPanel, 0, 9);
        layout.SetColumnSpan(testPanel, 2);

        dialog.ContentArea.Controls.Add(layout);

        // Dialog actions
        dialog.AddAction(isEdit ? "تحديث" : "إضافة", async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                RtlMessageBox.Show("يرجى إدخال اسم الطابعة", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int baudRate = int.TryParse(cmbBaudRate.SelectedItem?.ToString(), out var br) ? br : 9600;

            try
            {
                if (isEdit && existing != null)
                {
                    await _printerManagementService.UpdatePrinterAsync(new PrinterDto(
                        existing.PrinterId, existing.Name, existing.Type, existing.Connection,
                        existing.Connection == "Network" ? existing.Address : null,
                        existing.Connection != "Network" ? existing.Address : null,
                        int.Parse(existing.PaperWidth.Replace("mm", "")),
                        existing.Role, existing.Enabled));
                }
                else
                {
                    // Determine IP address and port from connection type
                    string? ip = cmbConnection.SelectedItem?.ToString() == "Network" ? txtIpAddress.Text : null;
                    string? port = cmbConnection.SelectedItem?.ToString() != "Network" ? txtPort.Text : null;
                    int paperWidth = int.Parse(cmbPaperWidth.SelectedItem?.ToString()?.Replace("mm", "") ?? "80");
                    var printerType = cmbType.SelectedItem?.ToString() ?? "Thermal";
                    var connType = cmbConnection.SelectedItem?.ToString() ?? "USB";
                    var role = cmbRole.SelectedItem?.ToString() ?? "Receipt";

                    var created = await _printerManagementService.AddPrinterAsync(
                        txtName.Text, printerType, connType, ip, port, paperWidth, role);

                    _printers.Add(new PrinterEntry
                    {
                        PrinterId = created.Id,
                        Name = txtName.Text,
                        Type = printerType,
                        Connection = connType,
                        Address = ip ?? port ?? "",
                        PaperWidth = $"{paperWidth}mm",
                        Role = role,
                        Enabled = chkEnabled.Checked,
                        BaudRate = baudRate
                    });
                }

                PopulatePrinterGrid();
                dialog.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[PrinterManagementForm] SavePrinter failed: {ex}");
                RtlMessageBox.Show("حدث خطأ أثناء حفظ الطابعة", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });
        dialog.AddAction("إلغاء", (s, e) => dialog.Close(), false);

        dialog.ShowDialog(this.FindForm());
    }

    /// <summary>
    /// Tests connection to a printer by building a temporary Printer entity and
    /// calling IPrinterService.GetPrinterStatus on a background thread.
    /// </summary>
    private async Task<PrinterStatus> TestPrinterConnectionAsync(
        string name, string type, string connection, string address, int baudRate)
    {
        // Build a temporary Printer entity from the dialog fields
        var tempPrinter = new Printer
        {
            Name = name,
            PrinterType = type switch
            {
                "Thermal" => PrinterType.Thermal,
                "DotMatrix" => PrinterType.DotMatrix,
                _ => PrinterType.Thermal
            },
            Connection = connection switch
            {
                "USB" => PrinterConnection.USB,
                "Network" => PrinterConnection.Network,
                "Serial" => PrinterConnection.Serial,
                _ => PrinterConnection.USB
            },
            IpAddress = connection == "Network" ? address : null,
            Port = connection == "Network" ? (int.TryParse(address, out var p) ? p : 9100) : 0,
            ConnectionString = connection != "Network" ? address : null,
            BaudRate = baudRate,
            IsActive = true
        };

        // Run GetPrinterStatus on thread-pool to avoid blocking UI
        return await Task.Run(() => _printerService.GetPrinterStatus(tempPrinter));
    }

    /// <summary>
    /// Updates the connection status label with color-coded text based on PrinterStatus.
    /// </summary>
    private void UpdateConnectionStatusLabel(PrinterStatus status)
    {
        if (_lblConnectionStatus == null || _lblConnectionStatus.IsDisposed) return;

        switch (status)
        {
            case PrinterStatus.Online:
                _lblConnectionStatus.Text = "✅ متصل — الطابعة جاهزة";
                _lblConnectionStatus.ForeColor = DesignTokens.Colors.Success;
                break;
            case PrinterStatus.Offline:
                _lblConnectionStatus.Text = "⚠️ غير متصل — تعذر الاتصال بالطابعة";
                _lblConnectionStatus.ForeColor = DesignTokens.Colors.Warning;
                break;
            case PrinterStatus.Error:
                _lblConnectionStatus.Text = "❌ خطأ — الطابعة في حالة خطأ";
                _lblConnectionStatus.ForeColor = DesignTokens.Colors.Error;
                break;
            case PrinterStatus.Printing:
                _lblConnectionStatus.Text = "🖨 قيد الطباعة — الطابعة تعمل حالياً";
                _lblConnectionStatus.ForeColor = DesignTokens.Colors.Info;
                break;
            case PrinterStatus.Unknown:
            default:
                _lblConnectionStatus.Text = "⚠️ حالة غير معروفة — تحقق من الإعدادات";
                _lblConnectionStatus.ForeColor = DesignTokens.Colors.Warning;
                break;
        }
    }

    // --- Station Dialog ---

    private void ShowStationDialog(KitchenStationEntry? existing)
    {
        var isEdit = existing != null;
        var dialog = new RtlDialog(isEdit ? "تعديل محطة" : "إضافة محطة جديدة", 450, 300);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 4,
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

        layout.Controls.Add(CreateDlgLabel("اسم المحطة:"), 0, 0);
        var txtName = new RtlTextBox { Text = existing?.StationName ?? "", Dock = DockStyle.Fill, IsRequired = true };
        layout.Controls.Add(txtName, 1, 0);

        layout.Controls.Add(CreateDlgLabel("الطابعة:"), 0, 1);
        var cmbPrinter = new RtlComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        cmbPrinter.Items.Add("بدون طابعة");
        foreach (var p in _printers.Where(p => p.Enabled))
            cmbPrinter.Items.Add(p.Name);
        cmbPrinter.SelectedItem = string.IsNullOrEmpty(existing?.PrinterName) ? "بدون طابعة" : existing.PrinterName;
        layout.Controls.Add(cmbPrinter, 1, 1);

        layout.Controls.Add(CreateDlgLabel("مفعّلة:"), 0, 2);
        var chkActive = new CheckBox { Text = "تفعيل المحطة", RightToLeft = RightToLeft.Yes, Font = DesignTokens.Typography.Body, Checked = existing?.Active ?? true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        layout.Controls.Add(chkActive, 1, 2);

        dialog.ContentArea.Controls.Add(layout);

        dialog.AddAction(isEdit ? "تحديث" : "إضافة", async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                RtlMessageBox.Show("يرجى إدخال اسم المحطة", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedPrinter = cmbPrinter.SelectedItem?.ToString() == "بدون طابعة" ? "" : cmbPrinter.SelectedItem?.ToString() ?? "";
            var selectedPrinterId = _printers.FirstOrDefault(p => p.Name == selectedPrinter)?.PrinterId;

            try
            {
                if (!isEdit)
                {
                    var created = await _printerManagementService.AddKitchenStationAsync(txtName.Text, selectedPrinterId);
                    _kitchenStations.Add(new KitchenStationEntry
                    {
                        StationId = created.Id,
                        StationName = txtName.Text,
                        PrinterName = selectedPrinter,
                        Active = true
                    });
                }
                else if (existing != null)
                {
                    existing.StationName = txtName.Text;
                    existing.PrinterName = selectedPrinter;
                    existing.Active = chkActive.Checked;
                }

                PopulateStationGrid();
                dialog.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[PrinterManagementForm] SavePrinterStation failed: {ex}");
                RtlMessageBox.Show("حدث خطأ أثناء حفظ محطة الطباعة", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });
        dialog.AddAction("إلغاء", (s, e) => dialog.Close(), false);

        dialog.ShowDialog(this.FindForm());
    }

    // --- Actions ---

    /// <summary>
    /// Quick connection test from the grid context menu.
    /// Shows the status in a message box.
    /// </summary>
    private async Task TestPrinterConnectionQuickAsync(PrinterEntry printer)
    {
        try
        {
            var status = await TestPrinterConnectionAsync(
                printer.Name, printer.Type, printer.Connection, printer.Address, printer.BaudRate);

            var statusText = status switch
            {
                PrinterStatus.Online => "✅ متصل — الطابعة جاهزة",
                PrinterStatus.Offline => "⚠️ غير متصل",
                PrinterStatus.Error => "❌ حالة خطأ",
                PrinterStatus.Printing => "🖨 قيد الطباعة",
                _ => "⚠️ حالة غير معروفة"
            };

            var icon = status == PrinterStatus.Online ? MessageBoxIcon.Information
                     : status == PrinterStatus.Error ? MessageBoxIcon.Error
                     : MessageBoxIcon.Warning;

            RtlMessageBox.Show($"الطابعة: {printer.Name}\nالحالة: {statusText}",
                "اختبار الاتصال", MessageBoxButtons.OK, icon);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[PrinterManagementForm] TestPrinterConnectionQuickAsync failed: {ex}");
        }
    }

    private void TestPrinter(PrinterEntry printer)
    {
        RtlMessageBox.Show($"جاري إرسال صفحة اختبار إلى: {printer.Name}\nالرجاء الانتظار...", "اختبار الطباعة",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void DeletePrinter(PrinterEntry printer)
    {
        var result = RtlDialog.ShowDestructiveConfirm(
            "حذف طابعة",
            $"هل أنت متأكد من حذف الطابعة \"{printer.Name}\"؟"
        );
        if (result == DialogResult.OK)
        {
            _printers.Remove(printer);
            PopulatePrinterGrid();
        }
    }

    private void DeleteStation(KitchenStationEntry station)
    {
        var result = RtlDialog.ShowDestructiveConfirm(
            "حذف محطة",
            $"هل أنت متأكد من حذف محطة \"{station.StationName}\"؟"
        );
        if (result == DialogResult.OK)
        {
            _kitchenStations.Remove(station);
            PopulateStationGrid();
        }
    }

    // --- Helpers ---

    private Label CreateDlgLabel(string text)
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

    // --- Data Models ---

    private class PrinterEntry
    {
        public Guid PrinterId { get; set; }
        public string Name { get; set; } = "";
        public string Type { get; set; } = "Thermal";
        public string Connection { get; set; } = "USB";
        public string Address { get; set; } = "";
        public string PaperWidth { get; set; } = "80mm";
        public string Role { get; set; } = "Receipt";
        public bool Enabled { get; set; }
        public int BaudRate { get; set; } = 9600;
    }

    private class KitchenStationEntry
    {
        public Guid StationId { get; set; }
        public string StationName { get; set; } = "";
        public string PrinterName { get; set; } = "";
        public bool Active { get; set; }
    }
}