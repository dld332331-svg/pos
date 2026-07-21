using System.Drawing;
using System.Windows.Forms;
using POS.Desktop.Themes;
using POS.Desktop.CustomControls;

namespace POS.Desktop.Forms;

/// <summary>
/// HOLD-001: Dialog for holding and retrieving sales.
/// Two modes:
/// Mode 1 (Hold): Shows reason textbox + "تعليق الفاتورة" button.
/// Mode 2 (Retrieve): Shows DataGridView of held sales for current shift
/// (columns: وقت التعليق, السبب, المبلغ, إجراءات) with "استرجاع" button per row.
/// Delete held sale button. Empty state message.
/// Inherits from RtlDialog. All Arabic RTL.
/// </summary>
public class HoldSaleDialog : RtlDialog
{
    public enum HoldDialogMode
    {
        Hold,
        Retrieve
    }

    private enum HoldState
    {
        Ready,
        Empty,
        Loading,
        Error,
        PermissionDenied
    }

    private readonly HoldDialogMode _mode;
    private HoldState _currentState = HoldState.Ready;
    private List<HeldSaleEntry> _heldSales = new();

    // Mode 1 (Hold) Controls
    private RtlTextBox _txtHoldReason = null!;
    private Label _lblHoldValidation = null!;
    private Label _lblHoldInfo = null!;

    // Mode 2 (Retrieve) Controls
    private RtlDataGridView _heldSalesGrid = null!;
    private Panel _emptyPanel = null!;
    private Panel _permissionPanel = null!;
    private Label _lblCount = null!;

    // Results (Hold mode)
    public string HoldReason { get; private set; } = "";

    // Results (Retrieve mode)
    public Guid? RetrievedSaleId { get; private set; }

    // Events
    public event EventHandler<string>? SaleHeld;
    public event EventHandler<Guid>? SaleRetrieved;
    public event EventHandler<Guid>? SaleDeleted;

    /// <summary>
    /// Creates a HoldSaleDialog in Hold mode.
    /// Used when the user wants to hold the current sale.
    /// </summary>
    public HoldSaleDialog() : this(HoldDialogMode.Hold, new List<HeldSaleEntry>()) { }

    /// <summary>
    /// Creates a HoldSaleDialog in the specified mode.
    /// </summary>
    /// <param name="mode">Hold to suspend a sale, Retrieve to list and retrieve held sales.</param>
    /// <param name="existingHeldSales">List of currently held sales (for Retrieve mode).</param>
    public HoldSaleDialog(HoldDialogMode mode, List<HeldSaleEntry> existingHeldSales)
        : base(mode == HoldDialogMode.Hold ? "تعليق الفاتورة" : "استرجاع فاتورة معلقة", 650, 450)
    {
        _mode = mode;
        _heldSales = existingHeldSales ?? new List<HeldSaleEntry>();

        if (mode == HoldDialogMode.Hold)
        {
            ClientSize = new Size(450, 300);
        }

        InitializeComponent();
        SetState(HoldState.Ready);
    }

    private void InitializeComponent()
    {
        if (_mode == HoldDialogMode.Hold)
        {
            InitializeHoldMode();
        }
        else
        {
            InitializeRetrieveMode();
        }

        // ESC key closes dialog
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        };
    }

    // =====================================================
    // Mode 1: Hold - UI for suspending current sale
    // =====================================================

    private void InitializeHoldMode()
    {
        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 5,
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));

        // Info label
        _lblHoldInfo = new Label
        {
            Text = "سيتم حفظ الفاتورة الحالية مؤقتاً.\nيمكنك استرجاعها لاحقاً من نافذة الفواتير المعلقة.",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopRight,
            Height = 50
        };
        layout.Controls.Add(_lblHoldInfo, 0, 0);

        // Reason textbox
        var reasonPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Surface
        };

        var lblReason = new Label
        {
            Text = "سبب التعليق (اختياري):",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextPrimary,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Top,
            Height = 24
        };

        _txtHoldReason = new RtlTextBox
        {
            Multiline = true,
            RightToLeft = RightToLeft.Yes,
            Font = DesignTokens.Typography.Input,
            BackColor = DesignTokens.Colors.Surface,
            ForeColor = DesignTokens.Colors.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            PlaceholderText = "مثال: العميل ذهب لإحضار نقود..."
        };

        reasonPanel.Controls.Add(_txtHoldReason);
        reasonPanel.Controls.Add(lblReason);
        layout.Controls.Add(reasonPanel, 0, 1);

        // Validation label
        _lblHoldValidation = new Label
        {
            Text = "",
            Font = DesignTokens.Typography.Secondary,
            ForeColor = DesignTokens.Colors.Error,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopRight,
            Visible = false,
            Height = 20
        };
        layout.Controls.Add(_lblHoldValidation, 0, 2);

        // Empty spacer
        layout.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 3);

        // Another spacer
        layout.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 4);

        ContentArea.Controls.Add(layout);

        // Dialog actions
        AddAction("⏸️ تعليق الفاتورة", (s, e) => HoldSale(), true);
        AddAction("إلغاء", (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }, false);
    }

    // =====================================================
    // Mode 2: Retrieve - UI for listing and retrieving held sales
    // =====================================================

    private void InitializeRetrieveMode()
    {
        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard)
        };

        // Count label
        _lblCount = new Label
        {
            Text = $"الفواتير المعلقة: {_heldSales.Count}",
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleRight
        };
        mainPanel.Controls.Add(_lblCount);

        // Data Grid
        _heldSalesGrid = new RtlDataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true
        };

        _heldSalesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "وقت التعليق", Name = "HoldTime", FillWeight = 22 });
        _heldSalesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "السبب", Name = "Reason", FillWeight = 38 });
        _heldSalesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "المبلغ", Name = "Amount", FillWeight = 20 });
        _heldSalesGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "إجراءات", Name = "Actions", FillWeight = 20, Text = "استرجاع", UseColumnTextForButtonValue = true });

        _heldSalesGrid.CellClick += HeldSalesGrid_CellClick;
        _heldSalesGrid.CellFormatting += HeldSalesGrid_CellFormatting;

        mainPanel.Controls.Add(_heldSalesGrid);

        // Empty Panel
        _emptyPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Background,
            Visible = false
        };
        var emptyIcon = new Label
        {
            Text = "🗒️",
            Font = new Font("Segoe UI", 48f),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 80
        };
        var emptyLabel = new Label
        {
            Text = "لا توجد فواتير معلقة",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        var emptySubLabel = new Label
        {
            Text = "يمكنك تعليق فاتورة من شاشة نقطة البيع",
            Font = DesignTokens.Typography.Secondary,
            ForeColor = DesignTokens.Colors.TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Bottom,
            Height = 28
        };
        _emptyPanel.Controls.Add(emptySubLabel);
        _emptyPanel.Controls.Add(emptyLabel);
        _emptyPanel.Controls.Add(emptyIcon);

        mainPanel.Controls.Add(_emptyPanel);

        _permissionPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _permissionPanel.Controls.Add(new Label { Text = "ليس لديك صلاحية لعرض الفواتير المعلقة", Font = DesignTokens.Typography.SectionTitle, ForeColor = DesignTokens.Colors.TextSecondary, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });

        mainPanel.Controls.Add(_permissionPanel);

        ContentArea.Controls.Add(mainPanel);

        // Dialog actions
        AddAction("إغلاق", (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }, false);

        // Populate data
        PopulateGrid();
    }

    // --- State Management (Retrieve Mode) ---

    private void SetState(HoldState state)
    {
        _currentState = state;

        if (_mode == HoldDialogMode.Retrieve)
        {
            _emptyPanel.Visible = state == HoldState.Empty;
            _permissionPanel.Visible = state == HoldState.PermissionDenied;
            _heldSalesGrid.Visible = state == HoldState.Ready;
            _lblCount.Visible = state != HoldState.Empty;
        }
    }

    // --- Data Population (Retrieve Mode) ---

    private void PopulateGrid()
    {
        if (_mode != HoldDialogMode.Retrieve) return;

        _heldSalesGrid.Rows.Clear();
        foreach (var heldSale in _heldSales)
        {
            _heldSalesGrid.Rows.Add(
                heldSale.HoldTime.ToString("HH:mm:ss"),
                heldSale.Reason,
                DesignTokens.FormatJOD(heldSale.Amount) + " JOD",
                "استرجاع"
            );
            _heldSalesGrid.Rows[_heldSalesGrid.Rows.Count - 1].Tag = heldSale;
        }

        _lblCount.Text = $"الفواتير المعلقة: {_heldSales.Count}";
        SetState(_heldSales.Count > 0 ? HoldState.Ready : HoldState.Empty);
    }

    // --- Cell Formatting (Retrieve Mode) ---

    private void HeldSalesGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_heldSalesGrid.Columns[e.ColumnIndex].Name != "Amount") return;

        var heldSale = _heldSalesGrid.Rows[e.RowIndex].Tag as HeldSaleEntry;
        if (heldSale != null)
        {
            e.CellStyle.ForeColor = DesignTokens.Colors.Primary;
            e.CellStyle.Font = new Font(DesignTokens.Typography.Table, FontStyle.Bold);
        }
    }

    // --- Cell Click (Retrieve Mode) ---

    private void HeldSalesGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_heldSalesGrid.Columns[e.ColumnIndex].Name != "Actions") return;

        var heldSale = _heldSalesGrid.Rows[e.RowIndex].Tag as HeldSaleEntry;
        if (heldSale == null) return;

        var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };

        var retrieveItem = new ToolStripMenuItem("📥 استرجاع");
        retrieveItem.Click += (s, e) => RetrieveSale(heldSale);
        menu.Items.Add(retrieveItem);

        menu.Items.Add(new ToolStripSeparator());

        var deleteItem = new ToolStripMenuItem("🗑 حذف");
        deleteItem.Click += (s, e) => DeleteHeldSale(heldSale);
        menu.Items.Add(deleteItem);

        var cellRect = _heldSalesGrid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
        menu.Show(_heldSalesGrid, cellRect.Left, cellRect.Bottom);
    }

    // --- Hold Logic (Mode 1) ---

    private void HoldSale()
    {
        // Hold reason is optional, but if provided it should be trimmed
        HoldReason = _txtHoldReason.Text.Trim();

        // Raise event
        SaleHeld?.Invoke(this, HoldReason);

        DialogResult = DialogResult.OK;
        Close();
    }

    // --- Retrieve Logic (Mode 2) ---

    private void RetrieveSale(HeldSaleEntry heldSale)
    {
        var confirmResult = RtlDialog.ShowConfirm(
            "استرجاع فاتورة",
            $"هل تريد استرجاع الفاتورة المعلقة؟\nالمبلغ: {DesignTokens.FormatJOD(heldSale.Amount)} JOD\nالسبب: {heldSale.Reason}",
            "استرجاع",
            "إلغاء"
        );

        if (confirmResult == DialogResult.OK)
        {
            RetrievedSaleId = heldSale.Id;

            // Remove from list
            _heldSales.Remove(heldSale);
            PopulateGrid();

            // Raise event
            SaleRetrieved?.Invoke(this, heldSale.Id);

            if (_heldSales.Count == 0)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }

    // --- Delete Held Sale (Mode 2) ---

    private void DeleteHeldSale(HeldSaleEntry heldSale)
    {
        var deleteResult = RtlDialog.ShowDestructiveConfirm(
            "حذف فاتورة معلقة",
            $"هل أنت متأكد من حذف الفاتورة المعلقة؟\nالمبلغ: {DesignTokens.FormatJOD(heldSale.Amount)} JOD\nالسبب: {heldSale.Reason}\n\nسيتم حذفها نهائياً."
        );

        if (deleteResult == DialogResult.OK)
        {
            _heldSales.Remove(heldSale);
            PopulateGrid();

            // Raise event
            SaleDeleted?.Invoke(this, heldSale.Id);

            if (_heldSales.Count == 0)
            {
                // Keep dialog open - user may want to close manually
                // but update to show empty state
                SetState(HoldState.Empty);
            }
        }
    }

    // --- Static Factory Methods ---

    /// <summary>
    /// Shows the Hold dialog for suspending a sale.
    /// Returns the hold reason (empty string if none provided), or null if cancelled.
    /// </summary>
    public static string? ShowHoldDialog(IWin32Window? owner)
    {
        using var dialog = new HoldSaleDialog(HoldDialogMode.Hold, new List<HeldSaleEntry>());
        var result = dialog.ShowDialog(owner);
        return result == DialogResult.OK ? dialog.HoldReason : null;
    }

    /// <summary>
    /// Shows the Retrieve dialog for listing and retrieving held sales.
    /// Returns the ID of the retrieved sale, or null if none retrieved.
    /// </summary>
    public static Guid? ShowRetrieveDialog(IWin32Window? owner, List<HeldSaleEntry> heldSales)
    {
        using var dialog = new HoldSaleDialog(HoldDialogMode.Retrieve, heldSales);
        var result = dialog.ShowDialog(owner);
        return result == DialogResult.OK ? dialog.RetrievedSaleId : null;
    }

    // --- Data Model ---

    public class HeldSaleEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime HoldTime { get; set; } = DateTime.Now;
        public string Reason { get; set; } = "";
        public decimal Amount { get; set; }
        public string SerializedCart { get; set; } = "";
        public int? ShiftId { get; set; }
    }
}