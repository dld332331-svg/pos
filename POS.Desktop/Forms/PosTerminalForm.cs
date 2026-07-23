using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Desktop.Icons;
using POS.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

using POS.Desktop.Themes;
namespace POS.Desktop.Forms;

/// <summary>
/// POS-001: Main POS terminal screen with full state management per spec Section 15.9.
/// 
/// Screen States (spec Section 15.9):
/// - EmptySale, ActiveSale, LoadingProduct, ProductNotFound, OutOfStock
/// - DiscountDialog, HoldSale, RetrieveSale, Payment
/// - PaymentSuccess, PaymentFailure, PrinterFailure, PermissionDenied
/// 
/// Layout:
/// ┌─────────────────────────────────────────────────────┐
/// │ Header: User | Shift | Register | Status            │
/// ├───────────────────────────┬─────────────────────────┤
/// │ Left (Products)           │ Right (Transaction)     │
/// │ Search / Barcode          │ Items Grid              │
/// │ Categories                │ Totals                  │
/// │ Product Grid              │ Actions (Hold/Cancel)   │
/// ├───────────────────────────┴─────────────────────────┤
/// │ Bottom Bar: Payment Buttons | Retrieve Button       │
/// └─────────────────────────────────────────────────────┘
/// </summary>
public class PosTerminalForm : UserControl
{
    // ========================================================================
    // State Machine - Matches spec Section 15.9
    // ========================================================================
    private enum PosState
    {
        EmptySale,        // Initial - no items in cart
        ActiveSale,       // Items in cart, ready for payment/hold/cancel
        LoadingProduct,   // Searching or adding product (async)
        ProductNotFound,  // Search returned zero results
        OutOfStock,       // Tapped product has no stock
        DiscountDialog,   // Discount dialog is open
        HoldSale,         // Sale is being put on hold
        RetrieveSale,     // Retrieving a held sale
        Payment,          // Payment dialog is open
        PaymentSuccess,   // Payment completed successfully
        PaymentFailure,   // Payment failed
        PrinterFailure,   // Printer error occurred
        PermissionDenied  // User lacks permission for the action
    }

    // ========================================================================
    // Dependencies
    // ========================================================================
    private ISaleService? _saleService;
    private IProductService? _productService;
    private IPrinterManagementService? _printerManagementService;
    private IServiceScope? _formScope;
    private List<ModifierGroupDto> _modifierGroups = new();
    private bool _modifierGroupsLoaded;
    private bool _receiptPrinted;

    // ========================================================================
    // State Fields
    // ========================================================================
    private PosState _currentState = PosState.EmptySale;
    private Guid _currentSaleId;
    private Guid _currentShiftId;
    private Guid _currentUserId;
    private List<SaleItemDto> _saleItems = new();
    private List<ProductDto> _allProducts = new();
    private List<CategoryDto> _categories = new();
    private Guid? _selectedCategoryId;
    private List<HeldSaleDto> _heldSalesCache = new();
    private string _lastSearchTerm = "";

    // ========================================================================
    // Events
    // ========================================================================
    public event EventHandler<PaymentRequest>? RequestPayment;
    public event EventHandler? RequestHold;
    public event EventHandler? RequestRetrieve;
    public event EventHandler<Guid>? SaleCompleted;
    public event EventHandler? RequestLock;
    public event EventHandler? RequestLogout;

    // ========================================================================
    // Fonts (Font Awesome + Arabic)
    // ========================================================================
    private static readonly Font IconFont16 = FontLoader.GetFontAwesomeSolid(16f);
    private static readonly Font IconFont24 = FontLoader.GetFontAwesomeSolid(24f);
    private static readonly Font IconFont48 = FontLoader.GetFontAwesomeSolid(48f);
    private static readonly Font IconFont12 = FontLoader.GetFontAwesomeSolid(12f);
    private static readonly Font ArabicFont10 = FontLoader.GetArabicFont(10f);
    private static readonly Font ArabicFont12 = FontLoader.GetArabicFont(12f, FontStyle.Bold);
    private static readonly Font ArabicFont16 = FontLoader.GetArabicFont(16f, FontStyle.Bold);
    private static readonly Font ArabicFont20 = FontLoader.GetArabicFont(20f, FontStyle.Bold);
    private static readonly Font ArabicFont24 = FontLoader.GetArabicFont(24f, FontStyle.Bold);

    // ========================================================================
    // Overlay Panels (for visual states)
    // ========================================================================
    private Panel _overlayPanel;          // Full-size transparent overlay
    private Panel _loadingOverlay;
    private Panel _productNotFoundOverlay;
    private Panel _outOfStockOverlay;
    private Panel _paymentSuccessOverlay;
    private Panel _paymentFailureOverlay;
    private Panel _printerFailureOverlay;
    private Panel _permissionDeniedOverlay;

    // ========================================================================
    // Right Panel Controls (Transaction)
    // ========================================================================
    private Panel _rightPanel;
    private Panel _invoiceHeaderPanel;
    private Label _invoiceNumberLabel;
    private Label _invoiceDateLabel;
    private Label _invoiceStatusLabel;
    private Label _invoiceItemsCountLabel;
    private DataGridView _itemsGrid;
    private Panel _totalsPanel;
    private Label _subtotalLabel;
    private Label _taxLabel;
    private Label _discountLabel;
    private Label _promotionsLabel;
    private Label _totalLabel;
    private Panel _actionButtonsPanel;
    private List<AppliedPromotionDto> _appliedPromotions = new();
    private Button _holdButton;
    private Button _cancelButton;
    private Button _discountButton;
    private Button _customerButton;

    // ========================================================================
    // Left Panel Controls (Products)
    // ========================================================================
    private Panel _leftPanel;
    private TextBox _searchTextBox;
    private POS.Domain.Interfaces.IBarcodeScannerService? _barcodeScanner;
    private Panel _categoryPanel;
    private FlowLayoutPanel _categoryFlowLayout;
    private FlowLayoutPanel _productGrid;

    // ========================================================================
    // Bottom Bar Controls
    // ========================================================================
    private Panel _bottomBar;
    private Button _cashPaymentButton;
    private Button _cardPaymentButton;
    private Button _retrieveButton;
    private Button _cashDrawerButton;
    private Label _statusBarLabel;

    // ========================================================================
    // Empty Sale State Panel (shown when no items)
    // ========================================================================
    private Panel _emptySalePanel;

    // ========================================================================
    // Timer for auto-dismissal of temporary states
    // ========================================================================
    private readonly Timer _autoDismissTimer = new Timer { Interval = 3000 };

    // ========================================================================
    // Dispose Pattern
    // ========================================================================
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _autoDismissTimer.Stop();
            _autoDismissTimer.Dispose();
            _formScope?.Dispose();
        }
        base.Dispose(disposing);
    }

    // ========================================================================
    // Constructor
    // ========================================================================
    public PosTerminalForm()
    {
        InitializeComponent();
        SetState(PosState.EmptySale);
        _autoDismissTimer.Tick += (s, e) =>
        {
            _autoDismissTimer.Stop();
            if (_currentState == PosState.PaymentSuccess)
            {
                ClearCurrentSale();
            }
            else if (_currentState == PosState.PaymentFailure ||
                     _currentState == PosState.OutOfStock ||
                     _currentState == PosState.PrinterFailure)
            {
                SetState(_saleItems.Count > 0 ? PosState.ActiveSale : PosState.EmptySale);
            }
        };

        if (AppServiceProvider.Provider != null)
        {
            _formScope = AppServiceProvider.Provider.CreateScope();
            var sp = _formScope.ServiceProvider;
            _saleService = sp.GetService(typeof(ISaleService)) as ISaleService;
            _productService = sp.GetService(typeof(IProductService)) as IProductService;
            _printerManagementService = sp.GetService(typeof(IPrinterManagementService)) as IPrinterManagementService;
            _currentUserId = AppServiceProvider.CurrentUserId;
        }
    }

    public PosTerminalForm(ISaleService saleService, IProductService productService,
                           IPrinterManagementService printerManagementService,
                           Guid userId, Guid shiftId) : this()
    {
        _saleService = saleService;
        _productService = productService;
        _printerManagementService = printerManagementService;
        _currentUserId = userId;
        _currentShiftId = shiftId;
    }

    // ========================================================================
    // UI Initialization
    // ========================================================================
    private void InitializeComponent()
    {
        RightToLeft = RightToLeft.Yes;
        BackColor = DesignTokens.Colors.Background;
        Font = ArabicFont10;
        Dock = DockStyle.Fill;
        DoubleBuffered = true;

        // === RIGHT PANEL (Transaction) ===
        _rightPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 460,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Small)
        };

        BuildInvoiceHeader();
        BuildItemsGrid();
        BuildTotalsPanel();
        BuildActionButtons();

        _rightPanel.Controls.Add(_itemsGrid);
        _rightPanel.Controls.Add(_actionButtonsPanel);
        _rightPanel.Controls.Add(_totalsPanel);
        _rightPanel.Controls.Add(_invoiceHeaderPanel);

        // === LEFT PANEL ===
        _leftPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Background,
            Padding = new Padding(DesignTokens.Spacing.Small)
        };

        BuildSearchBox();
        BuildCategoryPanel();
        BuildProductGrid();

        _leftPanel.Controls.Add(_productGrid);
        _leftPanel.Controls.Add(_categoryPanel);
        _leftPanel.Controls.Add(_searchTextBox);

        // === BOTTOM BAR ===
        _bottomBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = DesignTokens.Colors.PrimaryHover,
            Padding = new Padding(DesignTokens.Spacing.Standard)
        };

        BuildBottomBar();

        // === Empty Sale Panel (shown in the left panel area) ===
        BuildEmptySalePanel();
        _productGrid.Controls.Add(_emptySalePanel);

        // === Overlay Panel (for visual feedback states) ===
        _overlayPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(220, DesignTokens.Colors.Surface),
            Visible = false
        };

        BuildLoadingOverlay();
        BuildProductNotFoundOverlay();
        BuildOutOfStockOverlay();
        BuildPaymentSuccessOverlay();
        BuildPaymentFailureOverlay();
        BuildPrinterFailureOverlay();
        BuildPermissionDeniedOverlay();

        _overlayPanel.Controls.Add(_permissionDeniedOverlay);
        _overlayPanel.Controls.Add(_printerFailureOverlay);
        _overlayPanel.Controls.Add(_paymentFailureOverlay);
        _overlayPanel.Controls.Add(_paymentSuccessOverlay);
        _overlayPanel.Controls.Add(_outOfStockOverlay);
        _overlayPanel.Controls.Add(_productNotFoundOverlay);
        _overlayPanel.Controls.Add(_loadingOverlay);

        // Assemble main control
        Controls.Add(_overlayPanel);
        Controls.Add(_rightPanel);
        Controls.Add(_leftPanel);
        Controls.Add(_bottomBar);

        // Wire events
        _cashPaymentButton.Click += (s, e) => _ = InitiatePaymentAsync("Cash");
        _cardPaymentButton.Click += (s, e) => _ = InitiatePaymentAsync("Card");
        _holdButton.Click += (s, e) => _ = InitiateHoldAsync();
        _cancelButton.Click += (s, e) => _ = CancelSaleAsync();
        _retrieveButton.Click += async (s, e) => await ShowRetrieveDialog();
        _cashDrawerButton.Click += async (s, e) => await OpenCashDrawerAsync();
        _discountButton.Click += (s, e) => _ = ShowDiscountDialogAsync();
        _customerButton.Click += (s, e) => AssignCustomer();
        _searchTextBox.TextChanged += async (s, e) => await OnSearchTextChanged();
        _searchTextBox.KeyDown += SearchTextBox_KeyDown;
    }

    // ========================================================================
    // Sub-builders
    // ========================================================================

    private void BuildInvoiceHeader()
    {
        _invoiceHeaderPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = DesignTokens.Colors.Card,
            Padding = new Padding(DesignTokens.Spacing.Small)
        };

        _invoiceNumberLabel = new Label
        {
            Text = "فاتورة جديدة",
            Font = ArabicFont16,
            ForeColor = DesignTokens.Colors.TextPrimary,
            Dock = DockStyle.Right,
            Height = 30,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false,
            Width = 280
        };

        _invoiceDateLabel = new Label
        {
            Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
            Font = ArabicFont10,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Right,
            Height = 20,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false,
            Width = 150
        };

        _invoiceStatusLabel = new Label
        {
            Text = "جديد",
            Font = ArabicFont10,
            ForeColor = DesignTokens.Colors.Success,
            Dock = DockStyle.Left,
            Width = 70,
            Height = 26,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = DesignTokens.Colors.SuccessLight,
            Padding = new Padding(4)
        };

        _invoiceItemsCountLabel = new Label
        {
            Text = "0 أصناف",
            Font = ArabicFont10,
            ForeColor = DesignTokens.Colors.TextHint,
            Dock = DockStyle.Left,
            Width = 80,
            Height = 20,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _invoiceHeaderPanel.Controls.Add(_invoiceNumberLabel);
        _invoiceHeaderPanel.Controls.Add(_invoiceDateLabel);
        _invoiceHeaderPanel.Controls.Add(_invoiceStatusLabel);
        _invoiceHeaderPanel.Controls.Add(_invoiceItemsCountLabel);
    }

    private void BuildItemsGrid()
    {
        _itemsGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            BackgroundColor = DesignTokens.Colors.Surface,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = DesignTokens.Colors.Border,
            RightToLeft = RightToLeft.Yes,
            Font = ArabicFont10,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowTemplate = new DataGridViewRow { Height = 32 }
        };

        _itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "المنتج", Name = "Product", FillWeight = 32 });
        _itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الكمية", Name = "Qty", FillWeight = 8, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الوحدة", Name = "Unit", FillWeight = 8, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "السعر", Name = "Price", FillWeight = 14, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleLeft } });
        _itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "خصم", Name = "Discount", FillWeight = 10, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleLeft } });
        _itemsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الإجمالي", Name = "Total", FillWeight = 16, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f, FontStyle.Bold) } });

        var removeBtnCol = new DataGridViewButtonColumn
        {
            HeaderText = "",
            Name = "Actions",
            Text = FontAwesomeIcons.Delete,
            FillWeight = 6,
            FlatStyle = FlatStyle.Flat,
            UseColumnTextForButtonValue = true
        };
        _itemsGrid.Columns.Add(removeBtnCol);

        _itemsGrid.CellClick += ItemsGrid_CellClick;
        _itemsGrid.CellPainting += ItemsGrid_CellPainting;
        _itemsGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = DesignTokens.Colors.Card,
            ForeColor = DesignTokens.Colors.TextPrimary,
            Font = ArabicFont12,
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            Padding = new Padding(4)
        };
    }

    private void BuildTotalsPanel()
    {
        _totalsPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 135,
            BackColor = DesignTokens.Colors.Card,
            Padding = new Padding(DesignTokens.Spacing.Small, DesignTokens.Spacing.Standard, DesignTokens.Spacing.Small, DesignTokens.Spacing.Small)
        };

        _subtotalLabel = CreateTotalRow("المجموع الفرعي", "0.000 JOD", 0);
        _taxLabel = CreateTotalRow("الضريبة", "0.000 JOD", 26);
        _discountLabel = CreateTotalRow("الخصم", "0.000 JOD", 52);

        _promotionsLabel = new Label
        {
            Text = "",
            Font = ArabicFont12,
            ForeColor = DesignTokens.Colors.Warning,
            Location = new Point(10, 76),
            Size = new Size(420, 20),
            TextAlign = ContentAlignment.MiddleRight,
            Visible = false
        };

        var separator = new Panel
        {
            Location = new Point(10, 100),
            Size = new Size(420, 1),
            BackColor = DesignTokens.Colors.Border
        };

        _totalLabel = new Label
        {
            Text = "الإجمالي:  0.000 JOD",
            Font = ArabicFont20,
            ForeColor = DesignTokens.Colors.Primary,
            Location = new Point(10, 108),
            Size = new Size(420, 38),
            TextAlign = ContentAlignment.MiddleRight
        };

        _totalsPanel.Controls.Add(_subtotalLabel);
        _totalsPanel.Controls.Add(_taxLabel);
        _totalsPanel.Controls.Add(_discountLabel);
        _totalsPanel.Controls.Add(_promotionsLabel);
        _totalsPanel.Controls.Add(separator);
        _totalsPanel.Controls.Add(_totalLabel);
    }

    private void BuildActionButtons()
    {
        _actionButtonsPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 38,
            Padding = new Padding(0, DesignTokens.Spacing.Micro, 0, 0)
        };

        _holdButton = CreateActionButton($" {FontAwesomeIcons.Hold}  تجميد (F4)", DesignTokens.Colors.Info, DockStyle.Left);
        _cancelButton = CreateActionButton($" {FontAwesomeIcons.Cancel}  إلغاء (F5)", DesignTokens.Colors.Error, DockStyle.Left);
        _discountButton = CreateActionButton($" {FontAwesomeIcons.Discount}  خصم", DesignTokens.Colors.Warning, DockStyle.Left);
        _customerButton = CreateActionButton($" {FontAwesomeIcons.Customer}  عميل", DesignTokens.Colors.Success, DockStyle.Right);

        _actionButtonsPanel.Controls.Add(_holdButton);
        _actionButtonsPanel.Controls.Add(_cancelButton);
        _actionButtonsPanel.Controls.Add(_discountButton);
        _actionButtonsPanel.Controls.Add(_customerButton);
    }

    private void BuildSearchBox()
    {
        _searchTextBox = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 38,
            Font = ArabicFont12,
            RightToLeft = RightToLeft.Yes,
            PlaceholderText = $" {FontAwesomeIcons.Search}  بحث بالباركود أو اسم المنتج (F8)...",
            BackColor = DesignTokens.Colors.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Small)
        };
    }

    private void BuildCategoryPanel()
    {
        _categoryPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 46,
            BackColor = DesignTokens.Colors.Background,
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Small)
        };

        _categoryFlowLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoScroll = true,
            BackColor = DesignTokens.Colors.Background
        };

        _categoryPanel.Controls.Add(_categoryFlowLayout);
    }

    private void BuildProductGrid()
    {
        _productGrid = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            AutoScroll = true,
            BackColor = DesignTokens.Colors.Background,
            Padding = new Padding(DesignTokens.Spacing.Micro)
        };
    }

    private void BuildBottomBar()
    {
        _cashPaymentButton = CreateBottomButton(
            $" {FontAwesomeIcons.Cash}  نقدي (F2)",
            DesignTokens.Colors.Success, DockStyle.Right, false);

        _cardPaymentButton = CreateBottomButton(
            $" {FontAwesomeIcons.Card}  بطاقة (F3)",
            DesignTokens.Colors.Info, DockStyle.Right, false);

        _retrieveButton = CreateBottomButton(
            $" {FontAwesomeIcons.Retrieve}  استرجاع",
            DesignTokens.Colors.PrimaryHover, DockStyle.Left, true);

        _cashDrawerButton = CreateBottomButton(
            $" {FontAwesomeIcons.Money}  درج النقود",
            DesignTokens.Colors.CashDrawer, DockStyle.Left, true);

        // Status bar label in bottom bar
        _statusBarLabel = new Label
        {
            Text = "✓   جاهز",
            Font = ArabicFont10,
            ForeColor = DesignTokens.Colors.Disabled,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };

        _bottomBar.Controls.Add(_cashPaymentButton);
        _bottomBar.Controls.Add(_cardPaymentButton);
        _bottomBar.Controls.Add(_cashDrawerButton);
        _bottomBar.Controls.Add(_statusBarLabel);
        _bottomBar.Controls.Add(_retrieveButton);
    }

    private void BuildEmptySalePanel()
    {
        _emptySalePanel = new Panel
        {
            Size = new Size(600, 300),
            BackColor = DesignTokens.Colors.Background,
            Margin = new Padding(40)
        };

        var iconLabel = new Label
        {
            Text = FontAwesomeIcons.PosTerminal,
            Font = IconFont48,
            ForeColor = DesignTokens.Colors.TextHint,
            Dock = DockStyle.Top,
            Height = 80,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var titleLabel = new Label
        {
            Text = "نقطة البيع",
            Font = ArabicFont24,
            ForeColor = DesignTokens.Colors.TextPrimary,
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var subLabel = new Label
        {
            Text = "ابحث عن منتج أو امسح الباركود لبدء الفاتورة",
            Font = ArabicFont12,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var hintLabel = new Label
        {
            Text = $"F2: دفع نقدي   |   F3: بطاقة   |   F4: تعليق   |   F8: بحث   |   Esc: إلغاء",
            Font = ArabicFont10,
            ForeColor = DesignTokens.Colors.TextHint,
            Dock = DockStyle.Top,
            Height = 26,
            TextAlign = ContentAlignment.MiddleCenter,
            Top = 150
        };

        _emptySalePanel.Controls.Add(hintLabel);
        _emptySalePanel.Controls.Add(subLabel);
        _emptySalePanel.Controls.Add(titleLabel);
        _emptySalePanel.Controls.Add(iconLabel);
    }

    // ========================================================================
    // Overlay Builders
    // ========================================================================

    private void BuildLoadingOverlay()
    {
        _loadingOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(200, DesignTokens.Colors.Surface),
            Visible = false
        };

        var spinner = new Label
        {
            Text = FontAwesomeIcons.Loading,
            Font = IconFont48,
            ForeColor = DesignTokens.Colors.Primary,
            Dock = DockStyle.Top,
            Height = 80,
            TextAlign = ContentAlignment.MiddleCenter,
            Top = 150
        };

        var msg = new Label
        {
            Text = "جاري التحميل...",
            Font = ArabicFont16,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _loadingOverlay.Controls.Add(msg);
        _loadingOverlay.Controls.Add(spinner);
    }

    private void BuildProductNotFoundOverlay()
    {
        _productNotFoundOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(200, DesignTokens.Colors.Surface),
            Visible = false
        };

        var icon = new Label
        {
            Text = FontAwesomeIcons.Search,
            Font = IconFont48,
            ForeColor = DesignTokens.Colors.Warning,
            Dock = DockStyle.Top,
            Height = 80,
            TextAlign = ContentAlignment.MiddleCenter,
            Top = 120
        };

        var msg = new Label
        {
            Text = "المنتج غير موجود",
            Font = ArabicFont16,
            ForeColor = DesignTokens.Colors.TextPrimary,
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var subMsg = new Label
        {
            Text = "لم يتم العثور على منتج مطابق للبحث. تأكد من الاسم أو الباركود.",
            Font = ArabicFont10,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var dismissBtn = new Button
        {
            Text = $"{FontAwesomeIcons.Cancel}  إغلاق",
            Font = ArabicFont12,
            ForeColor = Color.White,
            BackColor = DesignTokens.Colors.Warning,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(140, 38),
            Location = new Point(250, 260),
            Cursor = Cursors.Hand
        };
        dismissBtn.Click += (s, e) => SetState(_saleItems.Count > 0 ? PosState.ActiveSale : PosState.EmptySale);

        _productNotFoundOverlay.Controls.Add(dismissBtn);
        _productNotFoundOverlay.Controls.Add(subMsg);
        _productNotFoundOverlay.Controls.Add(msg);
        _productNotFoundOverlay.Controls.Add(icon);
    }

    private void BuildOutOfStockOverlay()
    {
        _outOfStockOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(200, DesignTokens.Colors.Surface),
            Visible = false
        };

        var icon = new Label
        {
            Text = FontAwesomeIcons.Warning,
            Font = IconFont48,
            ForeColor = DesignTokens.Colors.Error,
            Dock = DockStyle.Top,
            Height = 80,
            TextAlign = ContentAlignment.MiddleCenter,
            Top = 120
        };

        var msg = new Label
        {
            Text = "المنتج غير متوفر في المخزون",
            Font = ArabicFont16,
            ForeColor = DesignTokens.Colors.Error,
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var subMsg = new Label
        {
            Text = "الكمية المتاحة غير كافية لإتمام العملية",
            Font = ArabicFont10,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _outOfStockOverlay.Controls.Add(subMsg);
        _outOfStockOverlay.Controls.Add(msg);
        _outOfStockOverlay.Controls.Add(icon);
    }

    private void BuildPaymentSuccessOverlay()
    {
        _paymentSuccessOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(220, DesignTokens.Colors.SuccessLight),
            Visible = false
        };

        var icon = new Label
        {
            Text = FontAwesomeIcons.Success,
            Font = IconFont48,
            ForeColor = DesignTokens.Colors.Success,
            Dock = DockStyle.Top,
            Height = 80,
            TextAlign = ContentAlignment.MiddleCenter,
            Top = 120
        };

        var msg = new Label
        {
            Text = "تم الدفع بنجاح!",
            Font = ArabicFont24,
            ForeColor = DesignTokens.Colors.Success,
            Dock = DockStyle.Top,
            Height = 45,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var changeLabel = new Label
        {
            Text = "",
            Name = "lblChangeAmount",
            Font = ArabicFont20,
            ForeColor = DesignTokens.Colors.TextPrimary,
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var autoDismissHint = new Label
        {
            Text = "سيتم العودة تلقائياً...",
            Font = ArabicFont10,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _paymentSuccessOverlay.Controls.Add(autoDismissHint);
        _paymentSuccessOverlay.Controls.Add(changeLabel);
        _paymentSuccessOverlay.Controls.Add(msg);
        _paymentSuccessOverlay.Controls.Add(icon);
    }

    private void BuildPaymentFailureOverlay()
    {
        _paymentFailureOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(220, DesignTokens.Colors.ErrorLight),
            Visible = false
        };

        var icon = new Label
        {
            Text = FontAwesomeIcons.Error,
            Font = IconFont48,
            ForeColor = DesignTokens.Colors.Error,
            Dock = DockStyle.Top,
            Height = 80,
            TextAlign = ContentAlignment.MiddleCenter,
            Top = 100
        };

        var msg = new Label
        {
            Text = "فشلت عملية الدفع",
            Font = ArabicFont16,
            ForeColor = DesignTokens.Colors.Error,
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var errorDetail = new Label
        {
            Text = "يرجى المحاولة مرة أخرى أو استخدام طريقة دفع مختلفة",
            Name = "lblErrorDetail",
            Font = ArabicFont10,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var retryBtn = new Button
        {
            Text = $"{FontAwesomeIcons.Retrieve}  إعادة المحاولة",
            Font = ArabicFont12,
            ForeColor = Color.White,
            BackColor = DesignTokens.Colors.Primary,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(160, 40),
            Location = new Point(200, 220),
            Cursor = Cursors.Hand
        };
        retryBtn.Click += (s, e) => SetState(_saleItems.Count > 0 ? PosState.ActiveSale : PosState.EmptySale);

        var cancelBtn = new Button
        {
            Text = $"{FontAwesomeIcons.Cancel}  إلغاء",
            Font = ArabicFont12,
            ForeColor = DesignTokens.Colors.TextPrimary,
            BackColor = DesignTokens.Colors.Border,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(140, 40),
            Location = new Point(370, 220),
            Cursor = Cursors.Hand
        };
        cancelBtn.Click += (s, e) => SetState(_saleItems.Count > 0 ? PosState.ActiveSale : PosState.EmptySale);

        _paymentFailureOverlay.Controls.Add(cancelBtn);
        _paymentFailureOverlay.Controls.Add(retryBtn);
        _paymentFailureOverlay.Controls.Add(errorDetail);
        _paymentFailureOverlay.Controls.Add(msg);
        _paymentFailureOverlay.Controls.Add(icon);
    }

    private void BuildPrinterFailureOverlay()
    {
        _printerFailureOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(220, DesignTokens.Colors.WarningLight),
            Visible = false
        };

        var icon = new Label
        {
            Text = FontAwesomeIcons.PrinterError,
            Font = IconFont48,
            ForeColor = DesignTokens.Colors.Warning,
            Dock = DockStyle.Top,
            Height = 80,
            TextAlign = ContentAlignment.MiddleCenter,
            Top = 100
        };

        var msg = new Label
        {
            Text = "فشلت الطباعة",
            Font = ArabicFont16,
            ForeColor = DesignTokens.Colors.Warning,
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var subMsg = new Label
        {
            Text = "تعذر الاتصال بالطابعة. يمكنك متابعة العمل أو المحاولة لاحقاً.",
            Font = ArabicFont10,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var retryBtn = new Button
        {
            Text = $"{FontAwesomeIcons.Print}  إعادة طباعة",
            Font = ArabicFont12,
            ForeColor = Color.White,
            BackColor = DesignTokens.Colors.Warning,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(160, 40),
            Location = new Point(200, 220),
            Cursor = Cursors.Hand
        };
        retryBtn.Click += (s, e) => _ = RetryPrintReceiptAsync();

        var dismissBtn = new Button
        {
            Text = $"{FontAwesomeIcons.Close}  تجاوز",
            Font = ArabicFont12,
            ForeColor = DesignTokens.Colors.TextPrimary,
            BackColor = DesignTokens.Colors.Border,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(140, 40),
            Location = new Point(370, 220),
            Cursor = Cursors.Hand
        };
        dismissBtn.Click += (s, e) =>
        {
            _receiptPrinted = true; // Bypass — mark as printed to avoid re-prompt
            if (_currentState == PosState.PrinterFailure)
            {
                SetState(PosState.PaymentSuccess);
                _autoDismissTimer.Interval = 1500;
                _autoDismissTimer.Start();
            }
        };

        _printerFailureOverlay.Controls.Add(dismissBtn);
        _printerFailureOverlay.Controls.Add(retryBtn);
        _printerFailureOverlay.Controls.Add(subMsg);
        _printerFailureOverlay.Controls.Add(msg);
        _printerFailureOverlay.Controls.Add(icon);
    }

    private void BuildPermissionDeniedOverlay()
    {
        _permissionDeniedOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(220, DesignTokens.Colors.Surface),
            Visible = false
        };

        var icon = new Label
        {
            Text = FontAwesomeIcons.Lock,
            Font = IconFont48,
            ForeColor = DesignTokens.Colors.Error,
            Dock = DockStyle.Top,
            Height = 80,
            TextAlign = ContentAlignment.MiddleCenter,
            Top = 120
        };

        var msg = new Label
        {
            Text = "ليست لديك صلاحية",
            Font = ArabicFont16,
            ForeColor = DesignTokens.Colors.Error,
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var subMsg = new Label
        {
            Text = "ليس لديك الصلاحية الكافية لإتمام هذه العملية. يرجى التواصل مع مدير النظام.",
            Font = ArabicFont10,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var dismissBtn = new Button
        {
            Text = $"{FontAwesomeIcons.Close}  إغلاق",
            Font = ArabicFont12,
            ForeColor = Color.White,
            BackColor = DesignTokens.Colors.Primary,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(140, 38),
            Location = new Point(250, 260),
            Cursor = Cursors.Hand
        };
        dismissBtn.Click += (s, e) => SetState(_saleItems.Count > 0 ? PosState.ActiveSale : PosState.EmptySale);

        _permissionDeniedOverlay.Controls.Add(dismissBtn);
        _permissionDeniedOverlay.Controls.Add(subMsg);
        _permissionDeniedOverlay.Controls.Add(msg);
        _permissionDeniedOverlay.Controls.Add(icon);
    }

    // ========================================================================
    // Helper: Create Controls
    // ========================================================================

    private static Label CreateTotalRow(string title, string value, int top)
    {
        return new Label
        {
            Text = $"{title}:  {value}",
            Font = DesignTokens.Typography.Table,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Location = new Point(10, top),
            Size = new Size(420, 24),
            TextAlign = ContentAlignment.MiddleRight
        };
    }

    private static Button CreateActionButton(string text, Color color, DockStyle dock)
    {
        return new Button
        {
            Text = text,
            Font = DesignTokens.Typography.Secondary,
            ForeColor = Color.White,
            BackColor = color,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(110, 32),
            Dock = dock,
            Cursor = Cursors.Hand,
            Margin = new Padding(DesignTokens.Spacing.Micro, 0, DesignTokens.Spacing.Micro, 0),
            UseVisualStyleBackColor = false,
            Enabled = false
        };
    }

    private static Button CreateBottomButton(string text, Color color, DockStyle dock, bool enabled)
    {
        return new Button
        {
            Text = text,
            Font = DesignTokens.Typography.ButtonBold,
            ForeColor = Color.White,
            BackColor = color,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(170, 44),
            Dock = dock,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Enabled = enabled,
            Margin = new Padding(DesignTokens.Spacing.Micro, 0, DesignTokens.Spacing.Micro, 0)
        };
    }

    // ========================================================================
    // STATE MACHINE — Core state management (spec Section 15.9)
    // ========================================================================
    private void SetState(PosState newState)
    {
        var oldState = _currentState;
        _currentState = newState;

        _autoDismissTimer.Stop();

        bool hasItems = _saleItems.Count > 0;

        // Hide all overlays first
        _overlayPanel.Visible = false;
        _loadingOverlay.Visible = false;
        _productNotFoundOverlay.Visible = false;
        _outOfStockOverlay.Visible = false;
        _paymentSuccessOverlay.Visible = false;
        _paymentFailureOverlay.Visible = false;
        _printerFailureOverlay.Visible = false;
        _permissionDeniedOverlay.Visible = false;

        // Show/hide empty sale panel
        if (_productGrid.Controls.Contains(_emptySalePanel))
            _productGrid.Controls.Remove(_emptySalePanel);

        switch (newState)
        {
            case PosState.EmptySale:
                _overlayPanel.Visible = false;
                _productGrid.Controls.Add(_emptySalePanel);
                _emptySalePanel.Visible = true;
                _cashPaymentButton.Enabled = false;
                _cardPaymentButton.Enabled = false;
                _holdButton.Enabled = false;
                _cancelButton.Enabled = false;
                _discountButton.Enabled = false;
                _customerButton.Enabled = false;
                _searchTextBox.Enabled = true;
                _statusBarLabel.Text = "✓   جاهز — ابدأ بالبحث عن منتج";
                _statusBarLabel.ForeColor = DesignTokens.Colors.Disabled;
                break;

            case PosState.ActiveSale:
                _overlayPanel.Visible = false;
                _cashPaymentButton.Enabled = true;
                _cardPaymentButton.Enabled = true;
                _holdButton.Enabled = true;
                _cancelButton.Enabled = true;
                _discountButton.Enabled = true;
                _customerButton.Enabled = true;
                _searchTextBox.Enabled = true;
                _statusBarLabel.Text = $"🛒   {_saleItems.Count} أصناف — {CalculateTotal():N3} JOD";
                _statusBarLabel.ForeColor = DesignTokens.Colors.Success;
                break;

            case PosState.LoadingProduct:
                _overlayPanel.Visible = true;
                _loadingOverlay.Visible = true;
                _loadingOverlay.BringToFront();
                _cashPaymentButton.Enabled = false;
                _cardPaymentButton.Enabled = false;
                _holdButton.Enabled = false;
                _cancelButton.Enabled = false;
                _searchTextBox.Enabled = false;
                _statusBarLabel.Text = "⏳   جاري تحميل المنتج...";
                _statusBarLabel.ForeColor = DesignTokens.Colors.Info;
                break;

            case PosState.ProductNotFound:
                _overlayPanel.Visible = true;
                _productNotFoundOverlay.Visible = true;
                _productNotFoundOverlay.BringToFront();
                _cashPaymentButton.Enabled = hasItems;
                _cardPaymentButton.Enabled = hasItems;
                _holdButton.Enabled = hasItems;
                _cancelButton.Enabled = hasItems;
                _searchTextBox.Enabled = true;
                _statusBarLabel.Text = "⚠️   المنتج غير موجود";
                _statusBarLabel.ForeColor = DesignTokens.Colors.Warning;
                break;

            case PosState.OutOfStock:
                _overlayPanel.Visible = true;
                _outOfStockOverlay.Visible = true;
                _outOfStockOverlay.BringToFront();
                _cashPaymentButton.Enabled = hasItems;
                _cardPaymentButton.Enabled = hasItems;
                _holdButton.Enabled = hasItems;
                _cancelButton.Enabled = hasItems;
                _searchTextBox.Enabled = true;
                _statusBarLabel.Text = "⚠️   المنتج غير متوفر";
                _statusBarLabel.ForeColor = DesignTokens.Colors.Error;
                _autoDismissTimer.Interval = 2000;
                _autoDismissTimer.Start();
                break;

            case PosState.DiscountDialog:
                _overlayPanel.Visible = false;
                _cashPaymentButton.Enabled = true;
                _cardPaymentButton.Enabled = true;
                _searchTextBox.Enabled = false;
                _statusBarLabel.Text = "💰   إدخال الخصم...";
                _statusBarLabel.ForeColor = DesignTokens.Colors.Warning;
                break;

            case PosState.HoldSale:
                _overlayPanel.Visible = false;
                _cashPaymentButton.Enabled = false;
                _cardPaymentButton.Enabled = false;
                _holdButton.Enabled = false;
                _cancelButton.Enabled = false;
                _searchTextBox.Enabled = false;
                _statusBarLabel.Text = "⏸   جاري تعليق الفاتورة...";
                _statusBarLabel.ForeColor = DesignTokens.Colors.Info;
                break;

            case PosState.RetrieveSale:
                _overlayPanel.Visible = false;
                _cashPaymentButton.Enabled = false;
                _cardPaymentButton.Enabled = false;
                _holdButton.Enabled = false;
                _cancelButton.Enabled = false;
                _searchTextBox.Enabled = false;
                _statusBarLabel.Text = "📂   جاري استرجاع الفاتورة...";
                _statusBarLabel.ForeColor = DesignTokens.Colors.Info;
                break;

            case PosState.Payment:
                _overlayPanel.Visible = false;
                _cashPaymentButton.Enabled = false;
                _cardPaymentButton.Enabled = false;
                _holdButton.Enabled = false;
                _cancelButton.Enabled = false;
                _discountButton.Enabled = false;
                _searchTextBox.Enabled = false;
                _statusBarLabel.Text = "💳   جاري معالجة الدفع...";
                _statusBarLabel.ForeColor = DesignTokens.Colors.Info;
                break;

            case PosState.PaymentSuccess:
                _overlayPanel.Visible = true;
                _paymentSuccessOverlay.Visible = true;
                _paymentSuccessOverlay.BringToFront();
                var changeLabel = _paymentSuccessOverlay.Controls.Find("lblChangeAmount", true).FirstOrDefault() as Label;
                if (changeLabel != null)
                {
                    var change = oldState == PosState.Payment ? CalculateChange() : 0;
                    changeLabel.Text = change > 0 ? $"الباقي: {change:N3} JOD" : "";
                }
                _cashPaymentButton.Enabled = false;
                _cardPaymentButton.Enabled = false;
                _statusBarLabel.Text = "✅   تم الدفع بنجاح!";
                _statusBarLabel.ForeColor = DesignTokens.Colors.Success;
                _autoDismissTimer.Interval = 3000;
                _autoDismissTimer.Start();
                break;

            case PosState.PaymentFailure:
                _overlayPanel.Visible = true;
                _paymentFailureOverlay.Visible = true;
                _paymentFailureOverlay.BringToFront();
                _cashPaymentButton.Enabled = true;
                _cardPaymentButton.Enabled = true;
                _statusBarLabel.Text = "❌   فشلت عملية الدفع";
                _statusBarLabel.ForeColor = DesignTokens.Colors.Error;
                break;

            case PosState.PrinterFailure:
                _overlayPanel.Visible = true;
                _printerFailureOverlay.Visible = true;
                _printerFailureOverlay.BringToFront();
                _cashPaymentButton.Enabled = false;
                _cardPaymentButton.Enabled = false;
                _statusBarLabel.Text = "🖨️   فشلت الطباعة — تحقق من الطابعة";
                _statusBarLabel.ForeColor = DesignTokens.Colors.Warning;
                break;

            case PosState.PermissionDenied:
                _overlayPanel.Visible = true;
                _permissionDeniedOverlay.Visible = true;
                _permissionDeniedOverlay.BringToFront();
                _cashPaymentButton.Enabled = false;
                _cardPaymentButton.Enabled = false;
                _statusBarLabel.Text = "🔒   ليست لديك صلاحية";
                _statusBarLabel.ForeColor = DesignTokens.Colors.Error;
                break;
        }

        OnStateChanged(oldState, newState);
    }

    /// <summary>
    /// Called when state transitions occur. Use for logging, audit, and side effects.
    /// </summary>
    private void OnStateChanged(PosState oldState, PosState newState)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[PosState] {oldState} → {newState}");
    }

    // ========================================================================
    // Public API — External callers (MainShell, etc.)
    // ========================================================================

    /// <summary>
    /// Handles keyboard shortcuts for the POS terminal.
    /// </summary>
    public void HandleKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.F2:
                if (_saleItems.Count > 0 && _currentState == PosState.ActiveSale)
                    _ = InitiatePaymentAsync("Cash");
                break;
            case Keys.F3:
                if (_saleItems.Count > 0 && _currentState == PosState.ActiveSale)
                    _ = InitiatePaymentAsync("Card");
                break;
            case Keys.F4:
                if (_saleItems.Count > 0 && _currentState == PosState.ActiveSale)
                    _ = InitiateHoldAsync();
                break;
            case Keys.F5:
                if (_saleItems.Count > 0 && (_currentState == PosState.ActiveSale || _currentState == PosState.EmptySale))
                    _ = CancelSaleAsync();
                break;
            case Keys.F8:
                _searchTextBox.Focus();
                _searchTextBox.SelectAll();
                break;
            case Keys.Escape:
                if (_overlayPanel.Visible)
                {
                    SetState(_saleItems.Count > 0 ? PosState.ActiveSale : PosState.EmptySale);
                }
                break;
        }
    }

    /// <summary>
    /// Called when payment succeeds (from PaymentDialog).
    /// </summary>
    public void OnPaymentSuccess(decimal changeAmount)
    {
        var changeLabel = _paymentSuccessOverlay.Controls.Find("lblChangeAmount", true).FirstOrDefault() as Label;
        if (changeLabel != null)
            changeLabel.Text = changeAmount > 0 ? $"الباقي: {changeAmount:N3} JOD" : "";

        _invoiceStatusLabel.Text = "تم الدفع";
        _invoiceStatusLabel.BackColor = DesignTokens.Colors.SuccessLight;
        SetState(PosState.PaymentSuccess);
        PlaySound(SoundEvent.PaymentSuccess);

        // Fire completed event with sale ID
        SaleCompleted?.Invoke(this, _currentSaleId);

        // Attempt to print receipt asynchronously (don't block the success flow)
        _ = PrintReceiptForCurrentSaleAsync();

        // After success animation, clear for a new sale
        _autoDismissTimer.Interval = 3000;
        _autoDismissTimer.Start();
    }

    /// <summary>
    /// Clears the current sale and resets to Empty state.
    /// </summary>
    public void ClearCurrentSale()
    {
        _saleItems.Clear();
        _currentSaleId = Guid.Empty;
        _receiptPrinted = false;
        _invoiceNumberLabel.Text = "فاتورة جديدة";
        _invoiceStatusLabel.Text = "جديد";
        _invoiceStatusLabel.BackColor = DesignTokens.Colors.WarningLight;
        _invoiceItemsCountLabel.Text = "0 أصناف";
        RefreshItemsGrid();
        RefreshTotals();
        SetState(PosState.EmptySale);
    }

    /// <summary>
    /// Attempts to print a receipt for the completed sale using the configured receipt printer.
    /// On success: stays in PaymentSuccess state.
    /// On failure: transitions to PrinterFailure state for user action.
    /// </summary>
    /// <summary>
    /// Attempts to print a receipt and kitchen tickets for the completed sale.
    /// Receipt printing is customer-facing — failure transitions to PrinterFailure.
    /// Kitchen ticket printing is operational — failures are logged but non-critical.
    /// </summary>
    private async Task PrintReceiptForCurrentSaleAsync()
    {
        if (_currentSaleId == Guid.Empty)
            return;

        _statusBarLabel.Text = "🖨️   جاري طباعة الإيصال...";
        _statusBarLabel.ForeColor = DesignTokens.Colors.Info;

        bool receiptSucceeded = false;

        try
        {
            if (_printerManagementService != null)
            {
                receiptSucceeded = await _printerManagementService.PrintReceiptAsync(_currentSaleId);

                if (receiptSucceeded)
                {
                    _receiptPrinted = true;
                    _statusBarLabel.Text = "✅   تمت طباعة الإيصال بنجاح";
                    _statusBarLabel.ForeColor = DesignTokens.Colors.Success;
                }
                else
                {
                    _receiptPrinted = false;
                    _statusBarLabel.Text = "🖨️⚠️   فشلت طباعة الإيصال — تحقق من الطابعة";
                    _statusBarLabel.ForeColor = DesignTokens.Colors.Warning;

                    // Only show printer failure if we're still in PaymentSuccess
                    if (_currentState == PosState.PaymentSuccess)
                    {
                        _autoDismissTimer.Stop();
                        SetState(PosState.PrinterFailure);
                        return; // Don't proceed to kitchen tickets
                    }
                }

                // If receipt succeeded, attempt kitchen ticket printing (non-critical)
                if (receiptSucceeded)
                {
                    _statusBarLabel.Text = "🖨️🍳   جاري طباعة تذاكر المطبخ...";
                    _statusBarLabel.ForeColor = DesignTokens.Colors.Info;

                    var kitchenSuccess = await _printerManagementService.PrintKitchenTicketsAsync(_currentSaleId);

                    if (kitchenSuccess)
                    {
                        _statusBarLabel.Text = "✅   تمت طباعة الإيصال وتذاكر المطبخ";
                        _statusBarLabel.ForeColor = DesignTokens.Colors.Success;
                    }
                    else
                    {
                        // Kitchen tickets failed but receipt succeeded — just log, don't block
                        _statusBarLabel.Text = "✅   تمت طباعة الإيصال (بعض تذاكر المطبخ لم تطبع)";
                        _statusBarLabel.ForeColor = DesignTokens.Colors.Success;
                        System.Diagnostics.Debug.WriteLine(
                            $"[PosPrinter] Some kitchen tickets failed for sale {_currentSaleId}");
                    }
                }
            }
            else
            {
                // No printer management service available — printing is optional
                _receiptPrinted = true;
                _statusBarLabel.Text = "✅   تم الدفع (لا توجد طابعة إيصالات)";
                _statusBarLabel.ForeColor = DesignTokens.Colors.Success;
            }
        }
        catch (Exception ex)
        {
            _receiptPrinted = false;
            _statusBarLabel.Text = "🖨️❌   خطأ في الطباعة";
            _statusBarLabel.ForeColor = DesignTokens.Colors.Error;
            System.Diagnostics.Debug.WriteLine(
                $"[PosPrinter] PrintReceiptForCurrentSaleAsync failed: {ex.Message}");

            if (_currentState == PosState.PaymentSuccess)
            {
                _autoDismissTimer.Stop();
                SetState(PosState.PrinterFailure);
            }
        }
    }

    /// <summary>
    /// Retries printing the receipt. Called from the PrinterFailure overlay retry button.
    /// </summary>
    private async Task RetryPrintReceiptAsync()
    {
        try
        {
            SetState(PosState.PaymentSuccess);
            _autoDismissTimer.Stop();
            await PrintReceiptForCurrentSaleAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[PosTerminalForm] RetryPrintReceiptAsync failed: {ex}");
        }
    }

    /// <summary>
    /// Updates the retrieve button badge with held sales count.
    /// </summary>
    public void UpdateRetrieveButton(int count)
    {
        _retrieveButton.Text = count > 0
            ? $" {FontAwesomeIcons.Retrieve}  معلقات ({count})"
            : $" {FontAwesomeIcons.Retrieve}  استرجاع";
        _retrieveButton.BackColor = count > 0
            ? DesignTokens.Colors.Success
            : DesignTokens.Colors.PrimaryHover;
    }

    /// <summary>
    /// Called when a product is successfully found and added.
    /// </summary>
    public async Task AddFoundProductAsync(ProductDto product)
    {
        await AddProductToSale(product);
    }

    // ========================================================================
    // Product Loading & Searching
    // ========================================================================

    public async Task LoadCategoriesAsync()
    {
        try
        {
            if (_productService != null)
            {
                _categories = await _productService.GetCategoriesAsync();
            }
            else
            {
                _categories = new List<CategoryDto>
                {
                    new(Guid.NewGuid(), "مشروبات ساخنة", null, 1, true, 12),
                    new(Guid.NewGuid(), "مشروبات باردة", null, 2, true, 8),
                    new(Guid.NewGuid(), "معجنات", null, 3, true, 6),
                    new(Guid.NewGuid(), "حلويات", null, 4, true, 10),
                    new(Guid.NewGuid(), "وجبات", null, 5, true, 15)
                };
            }

            BuildCategoryButtons();
        }
        catch { System.Diagnostics.Trace.TraceWarning("[POS] Failed to load categories from server, using sample data"); }
    }

    public async Task LoadProductsAsync()
    {
        SetState(PosState.LoadingProduct);
        try
        {
            if (_productService != null)
            {
                var result = await _productService.GetProductsAsync(new ProductFilterDto(null, null, null, "Active", 1, 200));
                _allProducts = result.Items;
            }
            else
            {
                _allProducts = GenerateSampleProducts();
            }

            await FilterProductsAsync(_searchTextBox.Text);
        }
        catch
        {
            _statusBarLabel.Text = "⚠️   فشل تحميل المنتجات";
            _statusBarLabel.ForeColor = DesignTokens.Colors.Error;
        }
        finally
        {
            SetState(_saleItems.Count > 0 ? PosState.ActiveSale : PosState.EmptySale);
        }
    }

    public void InitializeBarcodeScanner()
    {
        if (_barcodeScanner is not null) return;
        _barcodeScanner = AppServiceProvider.Provider?.GetService<POS.Domain.Interfaces.IBarcodeScannerService>();
        if (_barcodeScanner is null) return;

        _barcodeScanner.BarcodeReceived += OnBarcodeScanned;
        _ = _barcodeScanner.StartAsync();
    }

    private async void OnBarcodeScanned(object? sender, string barcode)
    {
        if (IsDisposed || _searchTextBox.IsDisposed) return;

        try
        {
            _searchTextBox.BeginInvoke(async () =>
            {
                _searchTextBox.Text = barcode;
                _searchTextBox.SelectAll();

                if (_allProducts.Count == 0)
                    await LoadProductsAsync();

                var product = _allProducts.FirstOrDefault(p =>
                    p.Barcode?.Equals(barcode, StringComparison.OrdinalIgnoreCase) == true);

                if (product is not null)
                {
                    await AddProductToSale(product);
                    _searchTextBox.Clear();
                }
                else
                {
                    SetState(PosState.ProductNotFound);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[PosTerminalForm] Barcode scan handler failed: {ex}");
        }
    }

    private void BuildCategoryButtons()
    {
        _categoryFlowLayout.Controls.Clear();

        var allBtn = CreateCategoryButton("الكل", null);
        _categoryFlowLayout.Controls.Add(allBtn);

        foreach (var cat in _categories.Where(c => c.IsActive))
        {
            _categoryFlowLayout.Controls.Add(CreateCategoryButton(cat.Name, cat.Id));
        }
    }

    private Button CreateCategoryButton(string name, Guid? categoryId)
    {
        var btn = new Button
        {
            Text = name,
            Font = ArabicFont10,
            AutoSize = true,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(DesignTokens.Spacing.Micro, 0, DesignTokens.Spacing.Micro, 0),
            Cursor = Cursors.Hand,
            Tag = categoryId,
            BackColor = categoryId == null ? DesignTokens.Colors.Primary : DesignTokens.Colors.Surface,
            ForeColor = categoryId == null ? Color.White : DesignTokens.Colors.TextPrimary,
            UseVisualStyleBackColor = false
        };

        btn.Click += async (s, e) =>
        {
            _selectedCategoryId = categoryId;
            UpdateCategoryButtonStyles();
            await FilterProductsAsync(_searchTextBox.Text);
        };

        return btn;
    }

    private void UpdateCategoryButtonStyles()
    {
        foreach (Control ctrl in _categoryFlowLayout.Controls)
        {
            if (ctrl is Button btn)
            {
                var isSel = (Guid?)btn.Tag == _selectedCategoryId;
                btn.BackColor = isSel ? DesignTokens.Colors.Primary : DesignTokens.Colors.Surface;
                btn.ForeColor = isSel ? Color.White : DesignTokens.Colors.TextPrimary;
            }
        }
    }

    private async Task OnSearchTextChanged()
    {
        _lastSearchTerm = _searchTextBox.Text;
        await FilterProductsAsync(_lastSearchTerm);
    }

    private async Task FilterProductsAsync(string searchTerm)
    {
        _productGrid.Controls.Clear();
        _productGrid.SuspendLayout();

        var filtered = _allProducts.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            filtered = filtered.Where(p =>
                (p.ArabicName?.ToLower().Contains(term) == true) ||
                (p.EnglishName?.ToLower().Contains(term) == true) ||
                (p.Barcode?.ToLower().Contains(term) == true) ||
                (p.Sku?.ToLower().Contains(term) == true));
        }

        if (_selectedCategoryId.HasValue)
        {
            filtered = filtered.Where(p => p.CategoryId == _selectedCategoryId.Value);
        }

        var productList = filtered.ToList();

        if (productList.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                // Show product not found state if user searched
                // (but don't show overlay, just show empty label in grid)
                _productGrid.Controls.Add(CreateEmptyGridMessage(
                    FontAwesomeIcons.Search,
                    "لا توجد منتجات مطابقة",
                    "حاول بكلمة بحث مختلفة أو اختر تصنيفاً آخر"));
            }
            else
            {
                _productGrid.Controls.Add(_emptySalePanel);
            }
        }
        else
        {
            foreach (var product in productList)
            {
                _productGrid.Controls.Add(CreateProductCard(product));
            }
        }

        _productGrid.ResumeLayout();
        await Task.CompletedTask;
    }

    private Panel CreateEmptyGridMessage(string icon, string title, string subtitle)
    {
        var panel = new Panel
        {
            Size = new Size(450, 200),
            BackColor = DesignTokens.Colors.Background,
            Margin = new Padding(30)
        };

        panel.Controls.Add(new Label
        {
            Text = icon,
            Font = IconFont48,
            ForeColor = DesignTokens.Colors.TextHint,
            Dock = DockStyle.Top,
            Height = 70,
            TextAlign = ContentAlignment.MiddleCenter
        });
        panel.Controls.Add(new Label
        {
            Text = title,
            Font = ArabicFont16,
            ForeColor = DesignTokens.Colors.TextPrimary,
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = ContentAlignment.MiddleCenter
        });
        panel.Controls.Add(new Label
        {
            Text = subtitle,
            Font = ArabicFont10,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter
        });

        return panel;
    }

    private Panel CreateProductCard(ProductDto product)
    {
        var isOutOfStock = product.CurrentStock <= 0;
        var card = new Panel
        {
            Size = new Size(150, 140),
            BackColor = DesignTokens.Colors.Surface,
            Margin = new Padding(DesignTokens.Spacing.Micro),
            Padding = new Padding(DesignTokens.Spacing.Small),
            Cursor = isOutOfStock ? Cursors.No : Cursors.Hand,
            BorderStyle = BorderStyle.FixedSingle,
            Tag = product
        };

        var nameLabel = new Label
        {
            Text = product.ArabicName,
            Font = ArabicFont10,
            ForeColor = isOutOfStock ? DesignTokens.Colors.TextHint : DesignTokens.Colors.TextPrimary,
            Location = new Point(8, 8),
            Size = new Size(130, 38),
            TextAlign = ContentAlignment.TopRight,
            AutoEllipsis = true,
            Padding = new Padding(0)
        };

        var priceLabel = new Label
        {
            Text = $"{product.SellingPrice:N3} JOD",
            Font = DesignTokens.Typography.CardTitle,
            ForeColor = isOutOfStock ? DesignTokens.Colors.Disabled : DesignTokens.Colors.Primary,
            Location = new Point(8, 52),
            Size = new Size(130, 24),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var stockLabel = new Label
        {
            Text = isOutOfStock
                ? $"{FontAwesomeIcons.Warning}  غير متوفر"
                : $"المخزون: {product.CurrentStock:N0}",
            Font = DesignTokens.Typography.Caption,
            ForeColor = isOutOfStock ? DesignTokens.Colors.Error : DesignTokens.Colors.Success,
            Location = new Point(8, 80),
            Size = new Size(130, 20),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };

        var barcodeLabel = new Label
        {
            Text = product.Barcode ?? "",
            Font = DesignTokens.Typography.Caption,
            ForeColor = DesignTokens.Colors.TextHint,
            Location = new Point(8, 104),
            Size = new Size(130, 18),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };

        card.Controls.Add(nameLabel);
        card.Controls.Add(priceLabel);
        card.Controls.Add(stockLabel);
        card.Controls.Add(barcodeLabel);

        if (!isOutOfStock)
        {
            card.Click += (s, e) => _ = AddProductToSale(product);
        }

        return card;
    }

    private List<ProductDto> GenerateSampleProducts()
    {
        var products = new List<ProductDto>();
        var names = new (string, decimal)[] {
            ("قهوة عربية", 1.500m), ("شاي أحمر", 1.000m), ("كابتشينو", 2.000m),
            ("لاتيه", 2.250m), ("عصير برتقال", 1.500m), ("كرواسون", 0.800m),
            ("كيك شوكولاتة", 2.500m), ("سندويش دجاج", 2.750m), ("سلطة خضراء", 2.000m),
            ("ماء معدني", 0.500m), ("عصير تفاح", 1.250m), ("موفن", 1.000m)
        };

        foreach (var (name, price) in names)
        {
            products.Add(new ProductDto(
                Guid.NewGuid(), name, null, null, null,
                _categories.Count > 0 ? _categories[0].Id : Guid.Empty, "مشروبات ساخنة",
                "Simple", "قطعة", price * 0.5m, price, 16m, 5m, null, null, "Active", false, 20m
            ));
        }

        return products;
    }

    // ========================================================================
    // Sale Operations
    // ========================================================================

    private async Task AddProductToSale(ProductDto product)
    {
        // Guard: prevent adding during certain states
        if (_currentState == PosState.Payment ||
            _currentState == PosState.HoldSale ||
            _currentState == PosState.RetrieveSale ||
            _currentState == PosState.LoadingProduct)
            return;

        // Check stock
        if (product.CurrentStock <= 0)
        {
            SetState(PosState.OutOfStock);
            return;
        }

        // For weighted products, prompt for quantity with unit selection
        var quantity = 1m;
        string? selectedUnitSymbol = null;
        if (product.ProductType == "Weighted")
        {
            var result = PromptForQuantityWithUnit(this.FindForm()!, product.Name, "Weight");
            if (result == null) return; // user cancelled
            quantity = result.Value.quantity;
            selectedUnitSymbol = result.Value.unit;
            // Update the product's unit in the cache so the grid displays the selected unit
            var cachedProduct = _allProducts.FirstOrDefault(p => p.Id == product.Id);
            if (cachedProduct != null)
            {
                // Use reflection to update the Unit field on the ProductDto record
                // Since ProductDto is a record, we create a new instance with the updated unit
                // Note: ProductDto is a positional record, so we replace it in _allProducts
                var idx = _allProducts.IndexOf(cachedProduct);
                if (idx >= 0)
                {
                    _allProducts[idx] = cachedProduct with { Unit = result.Value.unit };
                }
            }
        }

        SetState(PosState.LoadingProduct);

        // Start new sale if idle
        if (_currentState == PosState.EmptySale || _currentSaleId == Guid.Empty)
        {
            await StartNewSaleAsync();
        }

        // Show modifier selection dialog if product supports modifiers
        List<ModifierSelectionDto>? modifierSelections = null;
        decimal modifierExtra = 0;
        string modifierSummary = "";

        if (product.AllowModifiers)
        {
            try
            {
                // Load modifier groups if not already cached
                if (!_modifierGroupsLoaded && _productService != null)
                {
                    _modifierGroups = await _productService.GetModifierGroupsAsync();
                    _modifierGroupsLoaded = true;
                }

                if (_modifierGroups.Count > 0)
                {
                    var result = ModifierSelectionDialog.ShowDialog(
                        this.FindForm()!, product, _modifierGroups);

                    if (result == null)
                    {
                        // User cancelled modifier selection — don't add the product
                        SetState(_saleItems.Count > 0 ? PosState.ActiveSale : PosState.EmptySale);
                        return;
                    }

                    modifierSelections = result.Selections;
                    modifierExtra = result.TotalExtra;
                    modifierSummary = result.Summary;
                }
            }
            catch
            {
                // If modifier loading or dialog fails, add product without modifiers
            }
        }

        try
        {
            if (_saleService != null)
            {
                await _saleService.AddItemAsync(_currentSaleId,
                    new AddItemRequest(product.Id, quantity, null, modifierSelections, selectedUnitSymbol));
            }
        }
                
        catch
        {
            // Service call failed — still add locally for offline resilience
        }

        // Add to local list using SaleCalculator for business logic
        var existing = _saleItems.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing != null && product.ProductType != "Weighted")
        {
            _saleItems.Remove(existing);
            var updated = SaleCalculator.CreateItem(
                existing.Id, existing.ProductId, existing.ProductName, existing.UnitPrice,
                existing.Quantity + 1, existing.Discount, existing.TaxRate, existing.Cost,
                existing.Notes, existing.ModifierSummary);

            // If modifiers are present, recalculate with modifier extra
            if (modifierExtra > 0)
            {
                updated = SaleCalculator.RecalculateItem(updated, modifierExtra);
                updated = updated with { ModifierSummary = modifierSummary };
            }

            _saleItems.Add(updated);
        }
        else
        {
            var newItem = SaleCalculator.CreateItem(null, product, quantity, 0);

            // If modifiers are present, recalculate with modifier extra
            if (modifierExtra > 0)
            {
                newItem = SaleCalculator.RecalculateItem(newItem, modifierExtra);
                newItem = newItem with { ModifierSummary = modifierSummary };
            }

            _saleItems.Add(newItem);
        }

        // Refresh promotions and UI state
        await RefreshPromotionsAsync();

        SetState(PosState.ActiveSale);
        PlaySound(SoundEvent.ProductAdded);
    }

    private async Task StartNewSaleAsync()
    {
        try
        {
            if (_saleService != null)
            {
                _currentSaleId = await _saleService.CreateNewSaleAsync(_currentUserId, _currentShiftId);
            }
            else
            {
                _currentSaleId = Guid.NewGuid();
            }
        }
        catch
        {
            _currentSaleId = Guid.NewGuid();
        }

        _saleItems.Clear();
        _invoiceNumberLabel.Text = $"فاتورة: {_currentSaleId:N8}";
        _invoiceStatusLabel.Text = "نشطة";
        _invoiceStatusLabel.BackColor = DesignTokens.Colors.SuccessLight;
        _invoiceItemsCountLabel.Text = "0 أصناف";
        await Task.CompletedTask;
    }

    private void RefreshItemsGrid()
    {
        _itemsGrid.Rows.Clear();

        foreach (var item in _saleItems)
        {
            // Look up the product's unit of measure from the product cache
            var product = _allProducts.FirstOrDefault(p => p.Id == item.ProductId);
            var unit = item.Unit ?? product?.Unit ?? "";

            _itemsGrid.Rows.Add(
                item.ProductName,
                item.Quantity,
                unit,
                item.UnitPrice,
                item.Discount,
                item.LineTotal
            );
        }

        _invoiceItemsCountLabel.Text = $"{_saleItems.Count} أصناف";
    }

    private void RefreshTotals()
    {
        var totals = SaleCalculator.CalculateTotals(_saleItems);

        _subtotalLabel.Text = $"المجموع الفرعي:  {totals.SubTotal:N3} JOD";
        _taxLabel.Text = $"الضريبة:  {totals.Tax:N3} JOD";
        _discountLabel.Text = $"الخصم:  {totals.Discount:N3} JOD";
        _totalLabel.Text = $"الإجمالي:  {totals.Total:N3} JOD";
    }

    private async Task RefreshPromotionsAsync()
    {
        if (_saleService == null || _currentSaleId == Guid.Empty) return;

        try
        {
            _appliedPromotions = await _saleService.GetAppliedPromotionsAsync(_currentSaleId);

            if (_appliedPromotions.Count > 0)
            {
                var promoTexts = _appliedPromotions.Select(p => $"🎉 {p.Name}");
                _promotionsLabel.Text = string.Join("\n", promoTexts);
                _promotionsLabel.Visible = true;
            }
            else
            {
                _promotionsLabel.Visible = false;
            }
        }
        catch
        {
            _promotionsLabel.Visible = false;
        }
    }


    private decimal CalculateTotal()
    {
        return SaleCalculator.GetTotal(_saleItems);
    }


    private decimal CalculateChange()
    {
        // Placeholder — actual change comes from payment dialog
        return 0;
    }

    // ========================================================================
    // Grid Event Handlers
    // ========================================================================

    private void ItemsGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_itemsGrid.Columns[e.ColumnIndex].Name != "Actions") return;

        var item = _saleItems.ElementAtOrDefault(e.RowIndex);
        if (item == null) return;

        // Build context menu
        var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };

        // "Edit Modifiers" option — only for products that allow modifiers
        var product = _allProducts.FirstOrDefault(p => p.Id == item.ProductId);
        if (product is { AllowModifiers: true })
        {
            var editModItem = new ToolStripMenuItem("🛠 تعديل التعديلات");
            var capturedRowIndex = e.RowIndex; // capture for closure
            editModItem.Click += async (s, args) =>
            {
                await EditItemModifiers(capturedRowIndex);
            };
            menu.Items.Add(editModItem);
            menu.Items.Add(new ToolStripSeparator());
        }

        var deleteItem = new ToolStripMenuItem("🗑 حذف");
        var deleteRowIndex = e.RowIndex;
        deleteItem.Click += async (s, args) =>
        {
            if (!await CheckPermissionAsync("CancelItem"))
            {
                SetState(PosState.PermissionDenied);
                return;
            }

            var itemToRemove = _saleItems.ElementAtOrDefault(deleteRowIndex);
            if (itemToRemove == null) return;

            _saleItems.RemoveAt(deleteRowIndex);
            RefreshItemsGrid();
            RefreshTotals();

            if (_saleItems.Count == 0)
                SetState(PosState.EmptySale);
            else
                SetState(PosState.ActiveSale);

            // Remove from service if available
            if (_saleService != null && _currentSaleId != Guid.Empty)
            {
                try { await _saleService.RemoveItemAsync(_currentSaleId, itemToRemove.Id ?? Guid.Empty); }
                catch { System.Diagnostics.Trace.TraceWarning("[POS] Failed to remove item from service (offline)"); }
            }
        };
        menu.Items.Add(deleteItem);

        var cellRect = _itemsGrid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
        menu.Show(_itemsGrid, cellRect.Left, cellRect.Bottom);
    }

    private async Task EditItemModifiers(int rowIndex)
    {
        var item = _saleItems.ElementAtOrDefault(rowIndex);
        if (item == null) return;

        var product = _allProducts.FirstOrDefault(p => p.Id == item.ProductId);
        if (product == null) return;

        // Load modifier groups if not already cached
        if (!_modifierGroupsLoaded && _productService != null)
        {
            try
            {
                _modifierGroups = await _productService.GetModifierGroupsAsync();
                _modifierGroupsLoaded = true;
            }
            catch
            {
                // If loading fails, can't edit modifiers
                return;
            }
        }

        if (_modifierGroups.Count == 0) return;

        // Show modifier selection dialog
        var result = ModifierSelectionDialog.ShowDialog(
            this.FindForm()!, product, _modifierGroups);

        if (result == null) return; // User cancelled

        // Recalculate the item with new modifier extras
        var updated = SaleCalculator.RecalculateItem(item, result.TotalExtra);
        updated = updated with { ModifierSummary = result.Summary };
        _saleItems[rowIndex] = updated;

        // Update in service if available
        if (_saleService != null && _currentSaleId != Guid.Empty && item.Id.HasValue)
        {
            try
            {
                await _saleService.ModifyItemAsync(_currentSaleId, item.Id.Value,
                    result.Selections.ToArray());
            }
            catch
            {
                // Service update failed — local state is already updated
            }
        }

        RefreshItemsGrid();
        RefreshTotals();
    }
    /// <summary>
    /// Opens the cash drawer after checking OpenCashDrawer permission.
    /// Sends the ESC/POS cash drawer kick command to the configured receipt printer.
    /// </summary>
    private async Task OpenCashDrawerAsync()
    {
        if (!await CheckPermissionAsync("OpenCashDrawer"))
        {
            SetState(PosState.PermissionDenied);
            return;
        }

        try
        {
            _statusBarLabel.Text = "💰   جاري فتح درج النقود...";
            _statusBarLabel.ForeColor = DesignTokens.Colors.Info;

            if (_printerManagementService != null)
            {
                var success = await _printerManagementService.OpenCashDrawerAsync();
                if (success)
                {
                    _statusBarLabel.Text = "✅   تم فتح درج النقود";
                    _statusBarLabel.ForeColor = DesignTokens.Colors.Success;
                }
                else
                {
                    _statusBarLabel.Text = "⚠️   فشل فتح درج النقود";
                    _statusBarLabel.ForeColor = DesignTokens.Colors.Warning;
                }
            }
            else
            {
                _statusBarLabel.Text = "✅   تم فتح درج النقود (محاكاة)";
                _statusBarLabel.ForeColor = DesignTokens.Colors.Success;
            }
        }
        catch (Exception ex)
        {
            _statusBarLabel.Text = "❌   خطأ في فتح درج النقود";
            _statusBarLabel.ForeColor = DesignTokens.Colors.Error;
            System.Diagnostics.Debug.WriteLine($"[CashDrawer] Error: {ex.Message}");
        }

        // Auto-dismiss the status message after 3 seconds
        await Task.Delay(3000);
        SetState(_saleItems.Count > 0 ? PosState.ActiveSale : PosState.EmptySale);
    }

    private async Task<bool> CheckPermissionAsync(string permission)
    {
        if (_currentUserId == Guid.Empty) return true;
        using var scope = AppServiceProvider.Provider?.CreateScope();
        if (scope == null) return true;
        var authService = scope.ServiceProvider.GetService(typeof(IAuthService)) as IAuthService;
        if (authService == null) return true;
        return await authService.HasPermissionAsync(_currentUserId, permission);
    }



    private void ItemsGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_itemsGrid.Columns[e.ColumnIndex] is DataGridViewButtonColumn)
        {
            // Use Font Awesome icon for delete button
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
            TextRenderer.DrawText(e.Graphics!, FontAwesomeIcons.Delete, IconFont12,
                e.CellBounds, DesignTokens.Colors.Error,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            e.Handled = true;
        }
    }

    // ========================================================================
    // Search
    // ========================================================================

    private void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            // Find first visible product card and add it
            var firstCard = _productGrid.Controls
                .OfType<Panel>()
                .FirstOrDefault(p => p.Tag is ProductDto);

            if (firstCard?.Tag is ProductDto product)
            {
                _ = AddProductToSale(product);
            }
            else if (!string.IsNullOrWhiteSpace(_searchTextBox.Text))
            {
                SetState(PosState.ProductNotFound);
            }
        }
    }

    // ========================================================================
    // Payment
    // ========================================================================

    private async Task InitiatePaymentAsync(string method)
    {
        try
        {
            if (_saleItems.Count == 0) return;
            if (_currentState != PosState.ActiveSale) return;

            if (!await CheckPermissionAsync("Sell"))
            {
                SetState(PosState.PermissionDenied);
                return;
            }

            SetState(PosState.Payment);
            RequestPayment?.Invoke(this, new PaymentRequest(_currentSaleId, CalculateTotal(), method, null));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[PosTerminalForm] InitiatePaymentAsync failed: {ex}");
        }
    }

    // ========================================================================
    // Hold Sale
    // ========================================================================

    private async Task InitiateHoldAsync()
    {
        if (_saleItems.Count == 0) return;
        if (_currentState != PosState.ActiveSale) return;

        if (!await CheckPermissionAsync("Sell"))
        {
            SetState(PosState.PermissionDenied);
            return;
        }

        SetState(PosState.HoldSale);

        // Notify external listeners (MainShell) that hold was requested
        RequestHold?.Invoke(this, EventArgs.Empty);

        // Show hold reason dialog
        var reason = HoldSaleDialog.ShowHoldDialog(ParentForm);
        if (reason == null)
        {
            // User cancelled
            SetState(PosState.ActiveSale);
            return;
        }

        try
        {
            var heldId = Guid.Empty;
            if (_saleService != null)
            {
                heldId = await _saleService.HoldSaleAsync(_currentSaleId, reason);
            }

            // Clear the current sale
            var total = CalculateTotal();

            // Add to held sales cache
            _heldSalesCache.Add(new HeldSaleDto(heldId, DateTime.Now, reason, total));
            UpdateRetrieveButton(_heldSalesCache.Count);

            ClearCurrentSale();
            _statusBarLabel.Text = $"⏸  تم تعليق الفاتورة ({(reason.Length > 0 ? reason : "بدون سبب")})";
            _statusBarLabel.ForeColor = DesignTokens.Colors.Info;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[PosTerminalForm] InitiateHoldAsync failed: {ex}");
            _statusBarLabel.Text = "⚠️   فشل تعليق الفاتورة";
            _statusBarLabel.ForeColor = DesignTokens.Colors.Error;
            SetState(PosState.ActiveSale);
        }
    }

    // ========================================================================
    // Retrieve Sale
    // ========================================================================

    private async Task ShowRetrieveDialog()
    {
        if (_currentState == PosState.Payment ||
            _currentState == PosState.HoldSale ||
            _currentState == PosState.LoadingProduct)
            return;

        // Refresh held sales from service
        try
            {
                if (_saleService != null)
                {
                    var heldSales = await _saleService.GetHeldSalesAsync(_currentShiftId);
                    _heldSalesCache = heldSales;
                }
            }
            catch { System.Diagnostics.Trace.TraceWarning("[POS] Failed to load held sales, using cache"); }

            // Convert to HeldSaleEntry list for the dialog
            var entries = _heldSalesCache
                .Select(h => new HoldSaleDialog.HeldSaleEntry
                {
                    Id = h.Id,
                    HoldTime = h.HeldAt,
                    Reason = h.HoldReason ?? "",
                    Amount = h.TotalAmount
                })
                .ToList();

        if (entries.Count == 0)
        {
            _statusBarLabel.Text = "📂   لا توجد فواتير معلقة";
            _statusBarLabel.ForeColor = DesignTokens.Colors.Info;
            return;
        }

        SetState(PosState.RetrieveSale);

        // Notify external listeners (MainShell) that retrieve was requested
        RequestRetrieve?.Invoke(this, EventArgs.Empty);

        var retrievedId = HoldSaleDialog.ShowRetrieveDialog(ParentForm, entries);
        if (retrievedId.HasValue)
        {
            _statusBarLabel.Text = "📂   تم استرجاع الفاتورة";
            _statusBarLabel.ForeColor = DesignTokens.Colors.Success;

            // Remove from cache
            _heldSalesCache.RemoveAll(h => h.Id == retrievedId.Value);
            UpdateRetrieveButton(_heldSalesCache.Count);

            SetState(PosState.ActiveSale);
        }
        else
        {
            SetState(_saleItems.Count > 0 ? PosState.ActiveSale : PosState.EmptySale);
        }
    }

    // ========================================================================
    // Cancel Sale
    // ========================================================================

    private async Task CancelSaleAsync()
    {
        try
        {
            if (_currentState != PosState.ActiveSale && _currentState != PosState.EmptySale)
                return;

            if (_saleItems.Count == 0 && _currentSaleId == Guid.Empty)
                return;

            if (!await CheckPermissionAsync("CancelInvoice"))
            {
                SetState(PosState.PermissionDenied);
                return;
            }

            // Confirm cancellation
            var confirmResult = RtlMessageBox.Show(
                "هل أنت متأكد من إلغاء الفاتورة الحالية؟",
                "إلغاء الفاتورة",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2,
                MessageBoxOptions.RtlReading);

            if (confirmResult != DialogResult.Yes) return;

            ClearCurrentSale();
            _statusBarLabel.Text = "🗑   تم إلغاء الفاتورة";
            _statusBarLabel.ForeColor = DesignTokens.Colors.Warning;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[PosTerminalForm] CancelSaleAsync failed: {ex}");
        }
    }

    // ========================================================================
    // Discount Dialog
    // ========================================================================

    private async Task ShowDiscountDialogAsync()
    {
        try
        {
            if (_saleItems.Count == 0) return;
            if (_currentState != PosState.ActiveSale) return;

            if (!await CheckPermissionAsync("ApplyDiscount"))
            {
                SetState(PosState.PermissionDenied);
                return;
            }

            SetState(PosState.DiscountDialog);

            // Simple discount dialog using input box
            var discountStr = Microsoft.VisualBasic.Interaction.InputBox(
                "أدخل قيمة الخصم:", "خصم", "0", -1, -1);

            if (decimal.TryParse(discountStr, out var discountAmount) && discountAmount > 0)
        {
            if (discountAmount > CalculateTotal())
                discountAmount = CalculateTotal();

            // Apply discount proportionally using SaleCalculator
            _saleItems = SaleCalculator.DistributeDiscount(_saleItems, discountAmount);

            RefreshItemsGrid();
            RefreshTotals();
            _statusBarLabel.Text = $"💰   تم تطبيق خصم: {discountAmount:N3} JOD";
            _statusBarLabel.ForeColor = DesignTokens.Colors.Warning;
        }

        SetState(PosState.ActiveSale);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[PosTerminalForm] ShowDiscountDialogAsync failed: {ex}");
        }
    }

    // ========================================================================
    // Customer Assignment
    // ========================================================================

    /// <summary>
    /// Shows a dialog prompting for a numeric quantity with a unit-of-measure selector.
    /// Returns (quantity, unit) or null if cancelled.
    /// </summary>
    private static (decimal quantity, string unit)? PromptForQuantityWithUnit(Form owner, string productName, string category)
    {
        using var dialog = new Form
        {
            Text = "إدخال الكمية",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new(340, 200),
            Font = DesignTokens.Typography.Body
        };

        // Product name label
        dialog.Controls.Add(new Label
        {
            Text = $"الكمية للمنتج: {productName}",
            Location = new(12, 12),
            AutoSize = true,
            Font = DesignTokens.Typography.Body
        });

        // Quantity input
        var txtQuantity = new TextBox
        {
            Location = new(12, 38),
            Width = 100,
            Font = DesignTokens.Typography.Body,
            TextAlign = HorizontalAlignment.Center
        };
        dialog.Controls.Add(txtQuantity);

        // Unit selector
        var unitSymbols = GetUnitSymbolsForCategory(category);
        var cmbUnit = new ComboBox
        {
            Location = new(120, 38),
            Width = 100,
            Font = DesignTokens.Typography.Body,
            DropDownStyle = ComboBoxStyle.DropDownList,
            RightToLeft = RightToLeft.Yes
        };
        cmbUnit.Items.AddRange(unitSymbols.ToArray());
        cmbUnit.SelectedIndex = 0;
        dialog.Controls.Add(cmbUnit);

        // Buttons
        var btnOk = new Button { Text = "موافق", DialogResult = DialogResult.OK, Location = new(140, 90), Width = 80 };
        var btnCancel = new Button { Text = "إلغاء", DialogResult = DialogResult.Cancel, Location = new(30, 90), Width = 80 };
        dialog.Controls.Add(btnOk);
        dialog.Controls.Add(btnCancel);
        dialog.AcceptButton = btnOk;
        dialog.CancelButton = btnCancel;

        if (dialog.ShowDialog(owner) != DialogResult.OK)
            return null;

        if (decimal.TryParse(txtQuantity.Text.Trim(), out var quantity) && quantity > 0)
        {
            var unitText = cmbUnit.SelectedItem?.ToString() ?? "";
            return (Math.Round(quantity, 3), unitText);
        }

        RtlMessageBox.Show(owner, "الرجاء إدخال كمية صحيحة أكبر من صفر.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return null;
    }

    /// <summary>
    /// Returns the list of available unit display symbols for a given category.
    /// </summary>
    private static List<string> GetUnitSymbolsForCategory(string category)
    {
        return category switch
        {
            "Weight" => new() { "كغ", "غ" },
            "Volume" => new() { "لتر", "مل" },
            "Count" => new() { "قطعة", "دزينة" },
            _ => new() { "قطعة" }
        };
    }

    private void AssignCustomer()
    {
        if (_currentState != PosState.ActiveSale) return;

        // Simple customer search dialog (placeholder — can be expanded)
        var customerName = Microsoft.VisualBasic.Interaction.InputBox(
            "أدخل اسم العميل (اختياري):", "تعيين عميل", "", -1, -1);

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            _statusBarLabel.Text = $"👤   تم تعيين العميل: {customerName}";
            _statusBarLabel.ForeColor = DesignTokens.Colors.Success;
        }
    }

    private static POS.Domain.Interfaces.ISoundService? _soundService;

    private static void PlaySound(SoundEvent soundEvent)
    {
        _soundService ??= AppServiceProvider.Provider?.GetService<POS.Domain.Interfaces.ISoundService>();
        _soundService?.Play(soundEvent);
    }
}

