using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Desktop.CustomControls;
using POS.Desktop.Icons;
using POS.Desktop.Themes;

namespace POS.Desktop.Forms;

/// <summary>
/// RET-001: Return Management Form (POS_EN.md §26 — Returns and Cancellations).
/// Allows users to look up a completed sale by invoice number, select items to return,
/// specify quantities, enter a reason, and process the return.
/// Full state machine: Initial → LoadingInvoice → InvoiceLoaded/InvoiceNotFound/InvalidInvoice
///   → ItemsSelected → Processing → Success/Error
/// </summary>
public class ReturnForm : UserControl
{
    // ── State Machine ──
    private enum ReturnState
    {
        Initial,
        LoadingInvoice,
        InvoiceLoaded,
        InvoiceNotFound,
        InvalidInvoice,
        ItemsSelected,
        Processing,
        Success,
        Error,
        PermissionDenied,
        Empty
    }

    private ReturnState _currentState = ReturnState.Initial;

    // ── Services ──
    private readonly ISaleService _saleService;

    // ── Data ──
    private SaleSummaryDto? _currentSale;
    private List<SaleItemDto> _saleItems = new();
    private readonly Dictionary<Guid, decimal> _returnQuantities = new();
    private decimal _totalRefundAmount;
    private int _selectedItemCount;

    // ── Layout Controls ──
    private readonly Panel _headerPanel;
    private readonly Label _headerTitle;
    private readonly Panel _mainPanel;

    // ── Invoice Lookup ──
    private readonly Panel _lookupPanel;
    private readonly RtlTextBox _txtInvoiceNumber;
    private readonly Button _btnSearch;
    private readonly Button _btnBrowse;

    // ── Invoice Info ──
    private readonly Panel _invoiceInfoPanel;
    private readonly Label _lblInvoiceNumber;
    private readonly Label _lblInvoiceDate;
    private readonly Label _lblInvoiceTotal;
    private readonly Label _lblInvoiceStatus;

    // ── Items Grid ──
    private readonly RtlDataGridView _itemsGrid;
    private readonly DataGridViewCheckBoxColumn _colSelect;
    private readonly DataGridViewTextBoxColumn _colProduct;
    private readonly DataGridViewTextBoxColumn _colSoldQty;
    private readonly DataGridViewTextBoxColumn _colReturnQty;
    private readonly DataGridViewTextBoxColumn _colUnitPrice;
    private readonly DataGridViewTextBoxColumn _colSubtotal;
    private readonly DataGridViewButtonColumn _colActions;

    // ── Summary ──
    private readonly Panel _summaryPanel;
    private readonly Label _lblSelectedCount;
    private readonly Label _lblRefundAmount;

    // ── Reason ──
    private readonly Panel _reasonPanel;
    private readonly RtlTextBox _txtReason;
    private readonly Label _lblValidation;

    // ── Actions ──
    private readonly Panel _actionsPanel;
    private readonly RtlButton _btnConfirm;
    private readonly RtlButton _btnCancel;

    // ── State Overlays ──
    private readonly Panel _loadingOverlay;
    private readonly Panel _emptyOverlay;
    private readonly Panel _errorOverlay;
    private readonly Panel _permissionOverlay;
    private readonly Panel _successOverlay;
    private readonly Label _successDetailLabel;

    // ── Loading label reference (avoids fragile Controls.OfType lookup) ──
    private readonly Label _loadingMessageLabel;

    // ── Timers ──
    private readonly Timer _spinnerTimer;
    private int _spinnerFrame;
    private readonly Label _lblSpinner;

    // ── Guard for EditingControlShowing (prevents multiple handler attachments) ──
    private bool _editingHandlerAttached;

    private static readonly string[] SpinnerChars = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };

    public ReturnForm(ISaleService saleService)
    {
        _saleService = saleService ?? throw new ArgumentNullException(nameof(saleService));

        RightToLeft = RightToLeft.Yes;
        BackColor = DesignTokens.Colors.Background;

        // ── Spinner Timer ──
        _spinnerTimer = new Timer { Interval = 100 };
        _spinnerTimer.Tick += (s, e) =>
        {
            _spinnerFrame = (_spinnerFrame + 1) % SpinnerChars.Length;
            _lblSpinner!.Text = SpinnerChars[_spinnerFrame];
        };

        // ══════════════════════════════════════════════════════════════
        // Header
        // ══════════════════════════════════════════════════════════════
        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard, 0, DesignTokens.Spacing.Standard, 0)
        };

        // Bottom border
        _headerPanel.Controls.Add(new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = DesignTokens.Colors.Border
        });

        _headerTitle = new Label
        {
            Text = $"  إرجاع مرتجع",
            Font = DesignTokens.Typography.PageTitle,
            ForeColor = DesignTokens.Colors.TextPrimary,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false,
            Width = 220,
            Height = 56
        };

        var headerSubtitle = new Label
        {
            Text = "البحث عن فاتورة وإرجاع أصنافها",
            Font = DesignTokens.Typography.Secondary,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false,
            Width = 200,
            Height = 56,
            Padding = new Padding(0, 4, 0, 0)
        };

        _headerPanel.Controls.Add(headerSubtitle);
        _headerPanel.Controls.Add(_headerTitle);

        // ══════════════════════════════════════════════════════════════
        // Main Content Area
        // ══════════════════════════════════════════════════════════════
        _mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Background,
            Padding = new Padding(DesignTokens.Spacing.Standard)
        };

        // ── Invoice Lookup Section ──
        _lookupPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard)
        };

        var lookupBorder = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = DesignTokens.Colors.Border };
        var lookupInner = new Panel { Dock = DockStyle.Fill, Height = 44 };

        var lblInvoice = new Label
        {
            Text = "رقم الفاتورة:",
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.TextPrimary,
            Location = new Point(620, 8),
            Size = new Size(100, 28),
            TextAlign = ContentAlignment.MiddleRight
        };

        _txtInvoiceNumber = new RtlTextBox
        {
            Location = new Point(420, 8),
            Size = new Size(190, 28),
            PlaceholderText = "أدخل رقم الفاتورة",
            Font = DesignTokens.Typography.Input
        };
        _txtInvoiceNumber.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _ = SearchInvoiceAsync();
            }
        };

        _btnSearch = new Button
        {
            Text = $" بحث",
            Font = DesignTokens.Typography.Button,
            FlatStyle = FlatStyle.Flat,
            BackColor = DesignTokens.Colors.Primary,
            ForeColor = Color.White,
            Size = new Size(100, 32),
            Location = new Point(310, 6),
            Cursor = Cursors.Hand,
            FlatAppearance = { BorderSize = 0 },
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnSearch.Click += async (s, e) => await SearchInvoiceAsync();
        _btnSearch.MouseEnter += (s, e) => _btnSearch.BackColor = DesignTokens.Colors.PrimaryHover;
        _btnSearch.MouseLeave += (s, e) => _btnSearch.BackColor = DesignTokens.Colors.Primary;

        _btnBrowse = new Button
        {
            Text = " سجل الفواتير",
            Font = DesignTokens.Typography.Button,
            FlatStyle = FlatStyle.Flat,
            BackColor = DesignTokens.Colors.Surface,
            ForeColor = DesignTokens.Colors.Primary,
            Size = new Size(120, 32),
            Location = new Point(180, 6),
            Cursor = Cursors.Hand,
            FlatAppearance = { BorderSize = 1, BorderColor = DesignTokens.Colors.Primary },
            TextAlign = ContentAlignment.MiddleCenter
        };
        _btnBrowse.Click += async (s, e) => await BrowseSalesHistoryAsync();
        _btnBrowse.MouseEnter += (s, e) => { _btnBrowse.BackColor = DesignTokens.Colors.Primary; _btnBrowse.ForeColor = Color.White; };
        _btnBrowse.MouseLeave += (s, e) => { _btnBrowse.BackColor = DesignTokens.Colors.Surface; _btnBrowse.ForeColor = DesignTokens.Colors.Primary; };

        lookupInner.Controls.Add(lblInvoice);
        lookupInner.Controls.Add(_txtInvoiceNumber);
        lookupInner.Controls.Add(_btnSearch);
        lookupInner.Controls.Add(_btnBrowse);

        _lookupPanel.Controls.Add(lookupInner);
        _lookupPanel.Controls.Add(lookupBorder);

        // ── Invoice Info Panel ──
        _invoiceInfoPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.FromArgb(239, 246, 255),
            Padding = new Padding(DesignTokens.Spacing.Standard, DesignTokens.Spacing.Small, DesignTokens.Spacing.Standard, DesignTokens.Spacing.Small),
            Visible = false
        };

        _lblInvoiceNumber = new Label
        {
            Text = "",
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.TextPrimary,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false,
            Width = 200
        };

        _lblInvoiceDate = new Label
        {
            Text = "",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false,
            Width = 160
        };

        _lblInvoiceTotal = new Label
        {
            Text = "",
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.Primary,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false,
            Width = 160
        };

        _lblInvoiceStatus = new Label
        {
            Text = "",
            Font = DesignTokens.Typography.Body,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false,
            Width = 120
        };

        _invoiceInfoPanel.Controls.Add(_lblInvoiceNumber);
        _invoiceInfoPanel.Controls.Add(_lblInvoiceDate);
        _invoiceInfoPanel.Controls.Add(_lblInvoiceTotal);
        _invoiceInfoPanel.Controls.Add(_lblInvoiceStatus);

        // ── Items Grid ──
        _itemsGrid = new RtlDataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = DesignTokens.Colors.Surface,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = DesignTokens.Colors.Border,
            RowTemplate = { Height = 36 }
        };

        _colSelect = new DataGridViewCheckBoxColumn
        {
            HeaderText = "تحديد",
            Name = "Select",
            Width = 60,
            FillWeight = 8
        };

        _colProduct = new DataGridViewTextBoxColumn
        {
            HeaderText = "المنتج",
            Name = "Product",
            FillWeight = 35,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };

        _colSoldQty = new DataGridViewTextBoxColumn
        {
            HeaderText = "الكمية المباعة",
            Name = "SoldQty",
            Width = 80,
            FillWeight = 12,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };

        _colReturnQty = new DataGridViewTextBoxColumn
        {
            HeaderText = "كمية الإرجاع",
            Name = "ReturnQty",
            Width = 80,
            FillWeight = 12,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };

        _colUnitPrice = new DataGridViewTextBoxColumn
        {
            HeaderText = "سعر الوحدة",
            Name = "UnitPrice",
            Width = 80,
            FillWeight = 12,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        _colSubtotal = new DataGridViewTextBoxColumn
        {
            HeaderText = "المبلغ المسترد",
            Name = "Subtotal",
            Width = 100,
            FillWeight = 15,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        _colActions = new DataGridViewButtonColumn
        {
            HeaderText = "",
            Name = "Actions",
            Width = 50,
            FillWeight = 6,
            Text = "✕",
            UseColumnTextForButtonValue = true,
            FlatStyle = FlatStyle.Flat
        };

        _itemsGrid.Columns.AddRange(new DataGridViewColumn[]
        {
            _colSelect, _colProduct, _colSoldQty, _colReturnQty,
            _colUnitPrice, _colSubtotal, _colActions
        });

        _itemsGrid.CellValueChanged += ItemsGrid_CellValueChanged;
        _itemsGrid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (_itemsGrid.CurrentCell is DataGridViewCheckBoxCell)
                _itemsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _itemsGrid.CellClick += ItemsGrid_CellClick;
        _itemsGrid.EditingControlShowing += ItemsGrid_EditingControlShowing;

        // ── Summary Panel ──
        _summaryPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard),
            Visible = false
        };

        var summaryBorder = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = DesignTokens.Colors.Border };

        _lblSelectedCount = new Label
        {
            Text = "0 أصناف محددة",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false,
            Width = 160
        };

        _lblRefundAmount = new Label
        {
            Text = "المبلغ المسترد: 0.000 JOD",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false,
            Width = 280
        };

        _summaryPanel.Controls.Add(_lblRefundAmount);
        _summaryPanel.Controls.Add(_lblSelectedCount);
        _summaryPanel.Controls.Add(summaryBorder);

        // ── Reason Panel ──
        _reasonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard, DesignTokens.Spacing.Small, DesignTokens.Spacing.Standard, DesignTokens.Spacing.Small),
            Visible = false
        };

        var reasonBorder = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = DesignTokens.Colors.Border };

        var lblReasonLabel = new Label
        {
            Text = "سبب الإرجاع *:",
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.TextPrimary,
            Location = new Point(600, 6),
            Size = new Size(110, 28),
            TextAlign = ContentAlignment.MiddleRight
        };

        _txtReason = new RtlTextBox
        {
            Location = new Point(120, 6),
            Size = new Size(470, 28),
            PlaceholderText = "أدخل سبب الإرجاع (مطلوب)",
            Font = DesignTokens.Typography.Input,
            IsRequired = true
        };

        _lblValidation = new Label
        {
            Text = "",
            Font = DesignTokens.Typography.Secondary,
            ForeColor = DesignTokens.Colors.Error,
            Location = new Point(120, 38),
            Size = new Size(470, 20),
            TextAlign = ContentAlignment.TopRight,
            Visible = false
        };

        _reasonPanel.Controls.Add(lblReasonLabel);
        _reasonPanel.Controls.Add(_txtReason);
        _reasonPanel.Controls.Add(_lblValidation);
        _reasonPanel.Controls.Add(reasonBorder);

        // ── Actions Panel ──
        _actionsPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard)
        };

        var actionsBorder = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = DesignTokens.Colors.Border };

        _btnConfirm = new RtlButton
        {
            Text = "تأكيد الإرجاع",
            Type = RtlButton.ButtonType.Primary,
            Height = DesignTokens.ControlHeight.Standard,
            Width = 160,
            Location = new Point(540, 8),
            Visible = false,
            Cursor = Cursors.Hand
        };
        _btnConfirm.Click += async (s, e) => await ProcessReturnAsync();

        _btnCancel = new RtlButton
        {
            Text = "إلغاء",
            Type = RtlButton.ButtonType.Secondary,
            Height = DesignTokens.ControlHeight.Standard,
            Width = 120,
            Location = new Point(410, 8),
            Visible = true,
            Cursor = Cursors.Hand
        };
        _btnCancel.Click += (s, e) => ResetForm();

        _actionsPanel.Controls.Add(_btnConfirm);
        _actionsPanel.Controls.Add(_btnCancel);
        _actionsPanel.Controls.Add(actionsBorder);

        // ══════════════════════════════════════════════════════════════
        // State Overlays
        // ══════════════════════════════════════════════════════════════

        // Loading overlay
        _loadingOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(240, DesignTokens.Colors.Surface),
            Visible = false
        };

        _loadingMessageLabel = new Label
        {
            Text = "جاري البحث عن الفاتورة...",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.TextPrimary,
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(0, 120, 0, 0)
        };

        _lblSpinner = new Label
        {
            Text = SpinnerChars[0],
            Font = new Font("Segoe UI", 32f),
            ForeColor = DesignTokens.Colors.Primary,
            Dock = DockStyle.Top,
            Height = 60,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(0, 20, 0, 0)
        };

        _loadingOverlay.Controls.Add(_loadingMessageLabel);
        _loadingOverlay.Controls.Add(_lblSpinner);

        // Empty overlay
        _emptyOverlay = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _emptyOverlay.Controls.Add(new Label
        {
            Text = "لم يتم العثور على فاتورة",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(0, 80, 0, 0)
        });
        _emptyOverlay.Controls.Add(new Label
        {
            Text = "يرجى التحقق من رقم الفاتورة والمحاولة مرة أخرى",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter
        });
        var emptyRetryBtn = new Button
        {
            Text = "محاولة جديدة",
            Font = DesignTokens.Typography.Button,
            FlatStyle = FlatStyle.Flat,
            BackColor = DesignTokens.Colors.Primary,
            ForeColor = Color.White,
            Size = new Size(160, 40),
            Location = new Point(280, 180),
            Cursor = Cursors.Hand,
            FlatAppearance = { BorderSize = 0 }
        };
        emptyRetryBtn.Click += (s, e) => ResetForm();
        _emptyOverlay.Controls.Add(emptyRetryBtn);

        // Error overlay
        _errorOverlay = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _errorOverlay.Controls.Add(new Label
        {
            Text = "حدث خطأ أثناء البحث",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.Error,
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(0, 80, 0, 0)
        });
        _errorOverlay.Controls.Add(new Label
        {
            Text = "يرجى التحقق من الاتصال والمحاولة مرة أخرى",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter
        });
        var errorRetryBtn = new Button
        {
            Text = "إعادة المحاولة",
            Font = DesignTokens.Typography.Button,
            FlatStyle = FlatStyle.Flat,
            BackColor = DesignTokens.Colors.Primary,
            ForeColor = Color.White,
            Size = new Size(160, 40),
            Location = new Point(280, 180),
            Cursor = Cursors.Hand,
            FlatAppearance = { BorderSize = 0 }
        };
        errorRetryBtn.Click += async (s, e) => await SearchInvoiceAsync();
        _errorOverlay.Controls.Add(errorRetryBtn);

        // Permission overlay
        _permissionOverlay = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _permissionOverlay.Controls.Add(new Label
        {
            Text = "ليس لديك صلاحية إجراء المرتجعات",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        });

        // Success overlay
        _successOverlay = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        var successIcon = new Label
        {
            Text = "✅",
            Font = new Font("Segoe UI Emoji", 48f),
            Dock = DockStyle.Top,
            Height = 80,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(0, 60, 0, 0)
        };
        var successMsg = new Label
        {
            Text = "تم إتمام الإرجاع بنجاح",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.Success,
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _successDetailLabel = new Label
        {
            Text = "",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var successNewBtn = new Button
        {
            Text = "إرجاع جديد",
            Font = DesignTokens.Typography.Button,
            FlatStyle = FlatStyle.Flat,
            BackColor = DesignTokens.Colors.Primary,
            ForeColor = Color.White,
            Size = new Size(160, 40),
            Location = new Point(280, 280),
            Cursor = Cursors.Hand,
            FlatAppearance = { BorderSize = 0 }
        };
        successNewBtn.Click += (s, e) => ResetForm();
        _successOverlay.Controls.Add(successNewBtn);
        _successOverlay.Controls.Add(_successDetailLabel);
        _successOverlay.Controls.Add(successMsg);
        _successOverlay.Controls.Add(successIcon);

        // ══════════════════════════════════════════════════════════════
        // Assemble Layout
        // ══════════════════════════════════════════════════════════════

        // Build bottom-up: actions + reason + summary + grid
        _mainPanel.Controls.Add(_itemsGrid);           // fill
        _mainPanel.Controls.Add(_summaryPanel);         // bottom
        _mainPanel.Controls.Add(_reasonPanel);          // bottom
        _mainPanel.Controls.Add(_actionsPanel);         // bottom
        _mainPanel.Controls.Add(_invoiceInfoPanel);    // top
        _mainPanel.Controls.Add(_lookupPanel);         // top

        // Overlays on top of mainPanel
        _mainPanel.Controls.Add(_successOverlay);
        _mainPanel.Controls.Add(_permissionOverlay);
        _mainPanel.Controls.Add(_errorOverlay);
        _mainPanel.Controls.Add(_emptyOverlay);
        _mainPanel.Controls.Add(_loadingOverlay);

        Controls.Add(_mainPanel);
        Controls.Add(_headerPanel);

        // ══════════════════════════════════════════════════════════════
        // Initial State
        // ══════════════════════════════════════════════════════════════
        SetState(ReturnState.Initial);
        _txtInvoiceNumber.Focus();
    }

    // ──────────────────────────────────────────────────────────────
    // State Management
    // ──────────────────────────────────────────────────────────────

    private void SetState(ReturnState state)
    {
        _currentState = state;

        // Hide all overlays
        _loadingOverlay.Visible = false;
        _emptyOverlay.Visible = false;
        _errorOverlay.Visible = false;
        _permissionOverlay.Visible = false;
        _successOverlay.Visible = false;

        switch (state)
        {
            case ReturnState.Initial:
                _mainPanel.Visible = true;
                _lookupPanel.Enabled = true;
                _txtInvoiceNumber.Enabled = true;
                _btnSearch.Enabled = true;
                _btnBrowse.Enabled = true;
                _invoiceInfoPanel.Visible = false;
                _itemsGrid.Visible = false;
                _summaryPanel.Visible = false;
                _reasonPanel.Visible = false;
        _btnConfirm.Visible = false;
        _spinnerTimer.Stop();
        _txtInvoiceNumber.Focus();
        break;

    case ReturnState.LoadingInvoice:
                _mainPanel.Visible = true;
                _lookupPanel.Enabled = false;
                _txtInvoiceNumber.Enabled = false;
                _btnSearch.Enabled = false;
                _btnBrowse.Enabled = false;
                _invoiceInfoPanel.Visible = false;
                _itemsGrid.Visible = false;
                _summaryPanel.Visible = false;
                _reasonPanel.Visible = false;
                _btnConfirm.Visible = false;
                _loadingOverlay.Visible = true;
                _loadingOverlay.BringToFront();
                _spinnerTimer.Start();
                break;

            case ReturnState.InvoiceLoaded:
                _mainPanel.Visible = true;
                _lookupPanel.Enabled = true;
                _txtInvoiceNumber.Enabled = true;
                _btnSearch.Enabled = true;
                _btnBrowse.Enabled = true;
                _invoiceInfoPanel.Visible = true;
                _itemsGrid.Visible = true;
                _summaryPanel.Visible = true;
                _reasonPanel.Visible = true;
                _btnConfirm.Visible = false;  // Hidden until items are selected
                _itemsGrid.Enabled = true;
                _txtReason.Enabled = true;
                _loadingOverlay.Visible = false;
                _spinnerTimer.Stop();
                break;

            case ReturnState.InvoiceNotFound:
                _mainPanel.Visible = true;
                _lookupPanel.Enabled = true;
                _txtInvoiceNumber.Enabled = true;
                _btnSearch.Enabled = true;
                _btnBrowse.Enabled = true;
                _invoiceInfoPanel.Visible = false;
                _itemsGrid.Visible = false;
                _summaryPanel.Visible = false;
                _reasonPanel.Visible = false;
                _btnConfirm.Visible = false;
                _spinnerTimer.Stop();
                _emptyOverlay.Visible = true;
                _emptyOverlay.Controls.OfType<Label>().First().Text = "لم يتم العثور على فاتورة بهذا الرقم";
                _emptyOverlay.BringToFront();
                break;

            case ReturnState.InvalidInvoice:
                _mainPanel.Visible = true;
                _lookupPanel.Enabled = true;
                _txtInvoiceNumber.Enabled = true;
                _btnSearch.Enabled = true;
                _btnBrowse.Enabled = true;
                _invoiceInfoPanel.Visible = false;
                _itemsGrid.Visible = false;
                _summaryPanel.Visible = false;
                _reasonPanel.Visible = false;
                _btnConfirm.Visible = false;
                _spinnerTimer.Stop();
                _emptyOverlay.Visible = true;
                _emptyOverlay.Controls.OfType<Label>().First().Text = "الفواتير المكتملة فقط يمكن إرجاعها";
                _emptyOverlay.BringToFront();
                break;

            case ReturnState.ItemsSelected:
                _mainPanel.Visible = true;
                _lookupPanel.Enabled = true;
                _invoiceInfoPanel.Visible = true;
                _itemsGrid.Visible = true;
                _summaryPanel.Visible = true;
                _reasonPanel.Visible = true;
                _btnConfirm.Visible = true;
                _btnConfirm.Enabled = true;
                _itemsGrid.Enabled = true;
                _txtReason.Enabled = true;
                _loadingOverlay.Visible = false;
                _spinnerTimer.Stop();
                break;

            case ReturnState.Processing:
                _mainPanel.Visible = true;
                _lookupPanel.Enabled = false;
                _txtInvoiceNumber.Enabled = false;
                _btnSearch.Enabled = false;
                _btnBrowse.Enabled = false;
                _btnConfirm.Enabled = false;
                _itemsGrid.Enabled = false;
                _txtReason.Enabled = false;
                _loadingMessageLabel.Text = "جاري معالجة الإرجاع...";
                _loadingOverlay.Visible = true;
                _loadingOverlay.BringToFront();
                _spinnerTimer.Start();
                break;

            case ReturnState.Success:
                _spinnerTimer.Stop();
                _loadingOverlay.Visible = false;
                _mainPanel.Visible = true;
                _successOverlay.Visible = true;
                _successOverlay.BringToFront();
                _successDetailLabel.Text = $"تم إرجاع {_selectedItemCount} أصناف بقيمة {_totalRefundAmount:N3} JOD";
                break;

            case ReturnState.Error:
                _spinnerTimer.Stop();
                _loadingOverlay.Visible = false;
                _mainPanel.Visible = true;
                _lookupPanel.Enabled = true;
                _txtInvoiceNumber.Enabled = true;
                _btnSearch.Enabled = true;
                _btnBrowse.Enabled = true;
                _btnConfirm.Enabled = true;
                _itemsGrid.Enabled = true;
                _txtReason.Enabled = true;
                _errorOverlay.Visible = true;
                _errorOverlay.BringToFront();
                break;

            case ReturnState.PermissionDenied:
                _mainPanel.Visible = false;
                _permissionOverlay.Visible = true;
                _permissionOverlay.BringToFront();
                _spinnerTimer.Stop();
                break;

            case ReturnState.Empty:
                _mainPanel.Visible = true;
                _lookupPanel.Enabled = true;
                _invoiceInfoPanel.Visible = true;
                _itemsGrid.Visible = false;
                _summaryPanel.Visible = false;
                _reasonPanel.Visible = false;
                _btnConfirm.Visible = false;
                _btnBrowse.Enabled = true;
                _spinnerTimer.Stop();
                _emptyOverlay.Visible = true;
                _emptyOverlay.Controls.OfType<Label>().First().Text = "لا توجد أصناف في هذه الفاتورة";
                _emptyOverlay.BringToFront();
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Invoice Lookup
    // ──────────────────────────────────────────────────────────────

    private async Task SearchInvoiceAsync()
    {
        var invoiceNumber = _txtInvoiceNumber.Text.Trim();
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            _lblValidation.Text = "يرجى إدخال رقم الفاتورة";
            _lblValidation.Visible = true;
            return;
        }

        _lblValidation.Visible = false;
        SetState(ReturnState.LoadingInvoice);

        try
        {
            var sale = await _saleService.GetSaleByInvoiceNumberAsync(invoiceNumber);

            if (sale is null)
            {
                SetState(ReturnState.InvoiceNotFound);
                return;
            }

            if (sale.Status != "Completed")
            {
                SetState(ReturnState.InvalidInvoice);
                return;
            }

            _currentSale = sale;
            _saleItems = (await _saleService.GetSaleItemsAsync(sale.SaleId)) ?? new List<SaleItemDto>();

            if (_saleItems.Count == 0)
            {
                SetState(ReturnState.Empty);
                return;
            }

            PopulateInvoiceInfo();
            PopulateItemsGrid();
            SetState(ReturnState.InvoiceLoaded);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[ReturnForm] Invoice search failed: {ex.Message}");
            SetState(ReturnState.Error);
        }
    }

    private async Task BrowseSalesHistoryAsync()
    {
        SetState(ReturnState.LoadingInvoice);

        try
        {
            var sales = await _saleService.GetSalesHistoryAsync(
                DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1), 1, 50);

            var completedSales = sales?.Where(s => s.Status == "Completed").ToList() ?? new List<SaleSummaryDto>();

            if (completedSales.Count == 0)
            {
                SetState(ReturnState.InvoiceNotFound);
                _emptyOverlay.Controls.OfType<Label>().First().Text = "لا توجد فواتير مكتملة في آخر 30 يوماً";
                _emptyOverlay.BringToFront();
                return;
            }

            // Show a simple selection dialog
            using var selectDialog = new Form
            {
                Text = "اختر فاتورة للإرجاع",
                Size = new Size(500, 400),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                BackColor = DesignTokens.Colors.Surface,
                MinimizeBox = false,
                MaximizeBox = false
            };

            var titleLabel = new Label
            {
                Text = "اختر الفاتورة التي تريد إرجاع أصنافها:",
                Font = DesignTokens.Typography.Body,
                ForeColor = DesignTokens.Colors.TextPrimary,
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(DesignTokens.Spacing.Standard, 0, DesignTokens.Spacing.Standard, 0)
            };

            var listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = DesignTokens.Typography.Body,
                RightToLeft = RightToLeft.Yes,
                IntegralHeight = false
            };

            foreach (var s in completedSales)
            {
                listBox.Items.Add(new
                {
                    Id = s.SaleId,
                    DisplayText = $"{s.InvoiceNumber}  —  {s.CreatedAt:yyyy/MM/dd HH:mm}  —  {s.TotalAmount:N3} JOD"
                });
            }
            listBox.DisplayMember = "DisplayText";
            listBox.ValueMember = "Id";

            var okBtn = new Button
            {
                Text = "اختيار",
                Font = DesignTokens.Typography.Button,
                FlatStyle = FlatStyle.Flat,
                BackColor = DesignTokens.Colors.Primary,
                ForeColor = Color.White,
                Size = new Size(100, 36),
                Location = new Point(360, 8),
                Cursor = Cursors.Hand,
                FlatAppearance = { BorderSize = 0 }
            };

            var cancelBtn = new Button
            {
                Text = "إلغاء",
                Font = DesignTokens.Typography.Button,
                FlatStyle = FlatStyle.Flat,
                BackColor = DesignTokens.Colors.Surface,
                ForeColor = DesignTokens.Colors.TextPrimary,
                Size = new Size(100, 36),
                Location = new Point(250, 8),
                Cursor = Cursors.Hand,
                FlatAppearance = { BorderSize = 1, BorderColor = DesignTokens.Colors.Border }
            };

            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = DesignTokens.Colors.Surface,
                Padding = new Padding(DesignTokens.Spacing.Standard)
            };
            bottomPanel.Controls.Add(okBtn);
            bottomPanel.Controls.Add(cancelBtn);

            selectDialog.Controls.Add(listBox);
            selectDialog.Controls.Add(titleLabel);
            selectDialog.Controls.Add(bottomPanel);

            selectDialog.AcceptButton = okBtn;
            selectDialog.CancelButton = cancelBtn;

            Guid? selectedSaleId = null;
            okBtn.Click += (s, e) =>
            {
                if (listBox.SelectedItem != null)
                {
                    var prop = listBox.SelectedItem.GetType().GetProperty("Id");
                    if (prop != null)
                        selectedSaleId = (Guid)prop.GetValue(listBox.SelectedItem)!;
                    if (selectedSaleId.HasValue && selectedSaleId.Value != Guid.Empty)
                        selectDialog.Close();
                }
            };
            cancelBtn.Click += (s, e) => selectDialog.Close();

            selectDialog.ShowDialog(this);

            if (selectedSaleId.HasValue && selectedSaleId.Value != Guid.Empty)
            {
                var sale = completedSales.First(s => s.SaleId == selectedSaleId.Value);
                _txtInvoiceNumber.Text = sale.InvoiceNumber;
                await SearchInvoiceAsync();
                return;
            }

            // User cancelled
            SetState(_currentSale != null ? ReturnState.InvoiceLoaded : ReturnState.Initial);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[ReturnForm] Browse sales failed: {ex.Message}");
            SetState(ReturnState.Error);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Data Population
    // ──────────────────────────────────────────────────────────────

    private void PopulateInvoiceInfo()
    {
        if (_currentSale == null) return;

        var statusColor = _currentSale.Status switch
        {
            "Completed" => DesignTokens.Colors.Success,
            "Cancelled" => DesignTokens.Colors.Error,
            "Returned" => DesignTokens.Colors.Warning,
            _ => DesignTokens.Colors.Info
        };

        _lblInvoiceNumber.Text = $"رقم الفاتورة: {_currentSale.InvoiceNumber}";
        _lblInvoiceDate.Text = $"التاريخ: {_currentSale.CreatedAt:yyyy/MM/dd HH:mm}";
        _lblInvoiceTotal.Text = $"إجمالي الفاتورة: {_currentSale.TotalAmount:N3} JOD";
        _lblInvoiceStatus.Text = _currentSale.Status switch
        {
            "Completed" => "مكتملة ✓",
            "Cancelled" => "ملغاة ✕",
            "Returned" => "مرتجعة ↩",
            _ => _currentSale.Status
        };
        _lblInvoiceStatus.ForeColor = statusColor;
    }

    private void PopulateItemsGrid()
    {
        _itemsGrid.Rows.Clear();
        _returnQuantities.Clear();

        foreach (var item in _saleItems)
        {
            if (item.Id == null) continue;

            var soldQty = item.Quantity;
            var idx = _itemsGrid.Rows.Add(
                false,
                item.ProductName,
                soldQty,
                soldQty,  // Default return quantity = full quantity
                item.UnitPrice,
                soldQty * item.UnitPrice
            );

            _itemsGrid.Rows[idx].Tag = item;

            // Store initial return quantity
            _returnQuantities[item.Id!.Value] = soldQty;

            // Color the row
            _itemsGrid.Rows[idx].DefaultCellStyle.BackColor = DesignTokens.Colors.Surface;
        }

        UpdateSummary();
    }

    // ──────────────────────────────────────────────────────────────
    // Grid Events
    // ──────────────────────────────────────────────────────────────

    private void ItemsGrid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        var row = _itemsGrid.Rows[e.RowIndex];
        if (row.Tag is not SaleItemDto saleItem || saleItem.Id == null)
            return;

        var colName = _itemsGrid.Columns[e.ColumnIndex]?.Name;

        if (colName == "Select")
        {
            // Toggle selection highlight
            var isSelected = (bool)(row.Cells["Select"].Value ?? false);
            UpdateRowState(row, saleItem, isSelected);
            UpdateSummary();
        }
        else if (colName == "ReturnQty")
        {
            var qtyText = row.Cells["ReturnQty"].Value?.ToString() ?? "0";
            if (decimal.TryParse(qtyText, out var qty))
            {
                var soldQty = saleItem.Quantity;
                qty = Math.Max(0, Math.Min(qty, soldQty));
                row.Cells["ReturnQty"].Value = qty;
                _returnQuantities[saleItem.Id!.Value] = qty;

                var subtotal = qty * saleItem.UnitPrice;
                row.Cells["Subtotal"].Value = subtotal;

                // Auto-select if qty > 0
                var currentlySelected = (bool)(row.Cells["Select"].Value ?? false);
                if (qty > 0 && !currentlySelected)
                {
                    row.Cells["Select"].Value = true;
                }
                else if (qty == 0 && currentlySelected)
                {
                    row.Cells["Select"].Value = false;
                }

                // UpdateRowState will be triggered by the Select CellValueChanged if modified above
                UpdateSummary();
            }
        }
    }

    private void ItemsGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        if (_itemsGrid.Columns[e.ColumnIndex].Name == "Actions")
        {
            var row = _itemsGrid.Rows[e.RowIndex];
            if (row.Tag is not SaleItemDto saleItem || saleItem.Id == null)
                return;

            // Toggle select on action click
            var isSelected = (bool)(row.Cells["Select"].Value ?? false);
            row.Cells["Select"].Value = !isSelected;
        }
    }

    private void ItemsGrid_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
    {
        if (_itemsGrid.CurrentCell?.ColumnIndex != _colReturnQty.Index)
        {
            _editingHandlerAttached = false;
            return;
        }

        if (e.Control is TextBox tb && !_editingHandlerAttached)
        {
            _editingHandlerAttached = true;
            tb.KeyPress += ReturnQtyTextBox_KeyPress;
        }
    }

    private void ReturnQtyTextBox_KeyPress(object? sender, KeyPressEventArgs e)
    {
        // Allow digits, decimal point, and control chars
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '.')
        {
            e.Handled = true;
        }
    }

    private static void UpdateRowState(DataGridViewRow row, SaleItemDto saleItem, bool isSelected)
    {
        var backColor = isSelected
            ? Color.FromArgb(239, 246, 255)
            : DesignTokens.Colors.Surface;
        var foreColor = isSelected
            ? DesignTokens.Colors.Primary
            : DesignTokens.Colors.TextPrimary;

        foreach (DataGridViewCell cell in row.Cells)
        {
            cell.Style.BackColor = backColor;
            cell.Style.SelectionBackColor = backColor;
            cell.Style.ForeColor = foreColor;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Summary Calculation
    // ──────────────────────────────────────────────────────────────

    private void UpdateSummary()
    {
        _totalRefundAmount = 0;
        _selectedItemCount = 0;

        foreach (DataGridViewRow row in _itemsGrid.Rows)
        {
            if (row.Tag is not SaleItemDto saleItem || saleItem.Id == null)
                continue;

            var isSelected = (bool)(row.Cells["Select"].Value ?? false);
            if (!isSelected) continue;

            var qtyText = row.Cells["ReturnQty"].Value?.ToString() ?? "0";
            if (decimal.TryParse(qtyText, out var qty) && qty > 0)
            {
                _totalRefundAmount += qty * saleItem.UnitPrice;
                _selectedItemCount++;
            }
        }

        _lblSelectedCount.Text = $"{_selectedItemCount} أصناف محددة";
        _lblRefundAmount.Text = $"المبلغ المسترد: {_totalRefundAmount:N3} JOD";

        // Color the refund amount based on value
        if (_totalRefundAmount > 0)
        {
            _lblRefundAmount.ForeColor = DesignTokens.Colors.Primary;
            _btnConfirm.Visible = true;
        }
        else
        {
            _lblRefundAmount.ForeColor = DesignTokens.Colors.TextSecondary;
            _btnConfirm.Visible = false;
        }

        UpdateStateBasedOnSelection();
    }

    private void UpdateStateBasedOnSelection()
    {
        if (_currentState == ReturnState.InvoiceLoaded || _currentState == ReturnState.ItemsSelected)
        {
            var hasSelection = _itemsGrid.Rows.Cast<DataGridViewRow>()
                .Any(r => (bool)(r.Cells["Select"].Value ?? false));

            SetState(hasSelection ? ReturnState.ItemsSelected : ReturnState.InvoiceLoaded);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Process Return
    // ──────────────────────────────────────────────────────────────

    private async Task ProcessReturnAsync()
    {
        // Validate
        var reason = _txtReason.Text.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            _lblValidation.Text = "يرجى إدخال سبب الإرجاع";
            _lblValidation.Visible = true;
            _txtReason.Focus();
            return;
        }

        if (_totalRefundAmount <= 0)
        {
            _lblValidation.Text = "يرجى تحديد الأصناف المراد إرجاعها";
            _lblValidation.Visible = true;
            return;
        }

        _lblValidation.Visible = false;

        // Build return items list
        var returnItems = new List<ReturnItemRequest>();
        foreach (DataGridViewRow row in _itemsGrid.Rows)
        {
            if (row.Tag is not SaleItemDto saleItem || saleItem.Id == null)
                continue;

            var isSelected = (bool)(row.Cells["Select"].Value ?? false);
            if (!isSelected) continue;

            var qtyText = row.Cells["ReturnQty"].Value?.ToString() ?? "0";
            if (decimal.TryParse(qtyText, out var qty) && qty > 0)
            {
                returnItems.Add(new ReturnItemRequest(
                    saleItem.Id!.Value,
                    qty,
                    reason
                ));
            }
        }

        if (returnItems.Count == 0)
        {
            _lblValidation.Text = "يرجى تحديد الأصناف المراد إرجاعها";
            _lblValidation.Visible = true;
            return;
        }

        // Confirm
        var confirmResult = RtlDialog.ShowConfirm(
            "تأكيد الإرجاع",
            $"هل أنت متأكد من إرجاع {returnItems.Count} أصناف بقيمة {_totalRefundAmount:N3} JOD؟\nالسبب: {reason}\n\nهذا الإجراء لا يمكن التراجع عنه.",
            "تأكيد الإرجاع",
            "إلغاء"
        );

        if (confirmResult != DialogResult.OK)
            return;

        SetState(ReturnState.Processing);

        try
        {
            var result = await _saleService.ReturnItemsAsync(
                _currentSale!.SaleId,
                returnItems,
                reason
            );

            if (result.Success)
            {
                SetState(ReturnState.Success);
            }
            else
            {
                _lblValidation.Text = result.ErrorMessage ?? "فشلت عملية الإرجاع";
                _lblValidation.Visible = true;
                SetState(ReturnState.Error);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[ReturnForm] Return processing failed: {ex.Message}");
            _lblValidation.Text = "حدث خطأ أثناء معالجة الإرجاع. يرجى المحاولة مرة أخرى.";
            _lblValidation.Visible = true;
            SetState(ReturnState.Error);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Reset
    // ──────────────────────────────────────────────────────────────

    private void ResetForm()
    {
        _currentSale = null;
        _saleItems = new List<SaleItemDto>();
        _returnQuantities.Clear();
        _totalRefundAmount = 0;
        _txtInvoiceNumber.Text = "";
        _txtReason.Text = "";
        _lblValidation.Visible = false;
        _itemsGrid.Rows.Clear();
        _loadingMessageLabel.Text = "جاري البحث عن الفاتورة...";

        SetState(ReturnState.Initial);
    }

    // ──────────────────────────────────────────────────────────────
    // Permission check (called externally)
    // ──────────────────────────────────────────────────────────────

    public void SetPermissionDenied()
    {
        SetState(ReturnState.PermissionDenied);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _spinnerTimer?.Stop();
            _spinnerTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
