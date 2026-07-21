using System.Drawing;
using System.Windows.Forms;
using POS.Application.Services;
using POS.Application.DTOs;
using POS.Desktop.Themes;
using POS.Desktop.CustomControls;

namespace POS.Desktop.Forms;

public class KitchenDisplayForm : UserControl
{
    private enum KitchenDisplayState
    {
        Loading,
        Loaded,
        Empty,
        Error,
        PermissionDenied
    }

    private KitchenDisplayState _currentState = KitchenDisplayState.Loading;
    private readonly IKitchenOrderService _kitchenService;
    private List<KitchenOrderDto> _orders = new();
    private List<KitchenOrderDto> _filteredOrders = new();
    private HashSet<string> _readyOrderNumbers = new();
    private System.Windows.Forms.Timer _refreshTimer;

    private Panel _toolbarPanel;
    private Label _lblStationSelector;
    private RtlComboBox _cmbStation;
    private RtlButton _btnRefresh;
    private Label _lblPendingCount;
    private Label _lblLastRefresh;

    private FlowLayoutPanel _cardsPanel;
    private Panel _emptyOverlay;
    private Panel _loadingOverlay;
    private Panel _permissionPanel = null!;

    public event EventHandler<string>? OrderMarkedReady;

    public KitchenDisplayForm(IKitchenOrderService kitchenService)
    {
        _kitchenService = kitchenService;
        InitializeComponent();
        SetState(KitchenDisplayState.Loading);
        SetupRefreshTimer();
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
            Padding = new Padding(DesignTokens.Spacing.Standard),
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Compact)
        };

        var toolbarInner = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        _lblStationSelector = new Label
        {
            Text = "المحطة:",
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 0, DesignTokens.Spacing.Micro, 0)
        };

        _cmbStation = new RtlComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 180,
            Height = DesignTokens.ControlHeight.Standard,
            Margin = new Padding(0, 0, DesignTokens.Spacing.Standard, 0)
        };
        _cmbStation.Items.Add("جميع المحطات");
        _cmbStation.SelectedIndex = 0;
        _cmbStation.SelectedIndexChanged += (s, e) => ApplyStationFilter();

        _btnRefresh = new RtlButton
        {
            Text = "🔄 تحديث",
            Type = RtlButton.ButtonType.Secondary,
            Width = 100,
            Height = DesignTokens.ControlHeight.Standard,
            Margin = new Padding(0, 0, DesignTokens.Spacing.Standard, 0)
        };
        _btnRefresh.Click += async (s, e) => await LoadDataAsync();

        _lblPendingCount = new Label
        {
            Text = "المعلقة: ٠",
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.Warning,
            AutoSize = true,
            Margin = new Padding(DesignTokens.Spacing.Standard, 0, 0, 0)
        };

        _lblLastRefresh = new Label
        {
            Text = "",
            Font = DesignTokens.Typography.Secondary,
            ForeColor = DesignTokens.Colors.TextSecondary,
            AutoSize = true,
            Margin = new Padding(DesignTokens.Spacing.Small, 0, 0, 0)
        };

        toolbarInner.Controls.Add(_lblStationSelector);
        toolbarInner.Controls.Add(_cmbStation);
        toolbarInner.Controls.Add(_btnRefresh);
        toolbarInner.Controls.Add(_lblPendingCount);
        toolbarInner.Controls.Add(_lblLastRefresh);
        _toolbarPanel.Controls.Add(toolbarInner);

        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Background,
            AutoScroll = true,
            Padding = new Padding(DesignTokens.Spacing.Standard)
        };

        _cardsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            AutoScroll = true,
            BackColor = DesignTokens.Colors.Background,
            Padding = new Padding(DesignTokens.Spacing.Compact)
        };

        scrollPanel.Controls.Add(_cardsPanel);

        _loadingOverlay = ThemeManager.CreateLoadingPanel("جاري تحميل الطلبات المعلقة...");
        _loadingOverlay.Visible = false;

        _emptyOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Background,
            Visible = false
        };
        var emptyIcon = new Label
        {
            Text = "✅",
            Font = new Font("Segoe UI", 48f),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 80
        };
        var emptyLabel = new Label
        {
            Text = "لا توجد طلبات معلقة",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        _emptyOverlay.Controls.Add(emptyLabel);
        _emptyOverlay.Controls.Add(emptyIcon);

        _permissionPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _permissionPanel.Controls.Add(new Label { Text = "ليس لديك صلاحية لعرض شاشة المطبخ", Font = DesignTokens.Typography.SectionTitle, ForeColor = DesignTokens.Colors.TextSecondary, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });

        Controls.Add(_loadingOverlay);
        Controls.Add(_emptyOverlay);
        Controls.Add(_permissionPanel);
        Controls.Add(scrollPanel);
        Controls.Add(_toolbarPanel);
    }

    private void SetupRefreshTimer()
    {
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        _refreshTimer.Tick += async (s, e) => await LoadDataAsync();
        _refreshTimer.Start();
    }

    private void SetState(KitchenDisplayState state)
    {
        _currentState = state;
        _loadingOverlay.Visible = state == KitchenDisplayState.Loading;
        _emptyOverlay.Visible = state == KitchenDisplayState.Empty;
        _permissionPanel.Visible = state == KitchenDisplayState.PermissionDenied;
        _cardsPanel.Visible = state == KitchenDisplayState.Loaded;
        _btnRefresh.Enabled = state != KitchenDisplayState.Loading;
    }

    private async Task LoadDataAsync()
    {
        try
        {
            SetState(KitchenDisplayState.Loading);
            _orders = await _kitchenService.GetPendingOrdersAsync();

            var stations = await _kitchenService.GetStationsAsync();
            _cmbStation.Items.Clear();
            _cmbStation.Items.Add("جميع المحطات");
            foreach (var s in stations)
                _cmbStation.Items.Add(s);
            _cmbStation.SelectedIndex = 0;

            ApplyStationFilter();
            _lblLastRefresh.Text = $"آخر تحديث: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            SetState(KitchenDisplayState.Error);
            _cardsPanel.Controls.Clear();
            var errorLabel = new Label
            {
                Text = $"خطأ في تحميل الطلبات: {ex.Message}",
                Font = DesignTokens.Typography.Body,
                ForeColor = DesignTokens.Colors.Error,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _cardsPanel.Controls.Add(errorLabel);
            _cardsPanel.Visible = true;
        }
    }

    private void ApplyStationFilter()
    {
        var selectedStation = _cmbStation.SelectedItem?.ToString();

        if (selectedStation == null || selectedStation == "جميع المحطات")
        {
            _filteredOrders = _orders.Where(o => !_readyOrderNumbers.Contains(o.OrderNumber)).ToList();
        }
        else
        {
            _filteredOrders = _orders
                .Where(o => o.Station == selectedStation && !_readyOrderNumbers.Contains(o.OrderNumber))
                .ToList();
        }

        PopulateCards();
        _lblPendingCount.Text = $"المعلقة: {_filteredOrders.Count}";
        _lblPendingCount.ForeColor = _filteredOrders.Count > 5
            ? DesignTokens.Colors.Error
            : _filteredOrders.Count > 2
                ? DesignTokens.Colors.Warning
                : DesignTokens.Colors.Success;

        SetState(_filteredOrders.Count > 0 ? KitchenDisplayState.Loaded : KitchenDisplayState.Empty);
    }

    private void PopulateCards()
    {
        _cardsPanel.Controls.Clear();

        foreach (var order in _filteredOrders)
        {
            var card = CreateOrderCard(order);
            _cardsPanel.Controls.Add(card);
        }
    }

    private Panel CreateOrderCard(KitchenOrderDto order)
    {
        var cardWidth = 320;
        var elapsed = DateTime.UtcNow - order.OrderTime;
        var elapsedMinutes = (int)elapsed.TotalMinutes;

        var card = new Panel
        {
            Width = cardWidth,
            Height = 0,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(0),
            Margin = new Padding(DesignTokens.Spacing.Compact),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        if (order.IsPriority)
        {
            card.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, card.ClientRectangle,
                    DesignTokens.Colors.Error, 3, ButtonBorderStyle.Solid,
                    DesignTokens.Colors.Error, 3, ButtonBorderStyle.Solid,
                    DesignTokens.Colors.Error, 3, ButtonBorderStyle.Solid,
                    DesignTokens.Colors.Error, 3, ButtonBorderStyle.Solid);
            };
        }
        else
        {
            card.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, card.ClientRectangle,
                    DesignTokens.Colors.Border, 1, ButtonBorderStyle.Solid,
                    DesignTokens.Colors.Border, 1, ButtonBorderStyle.Solid,
                    DesignTokens.Colors.Border, 1, ButtonBorderStyle.Solid,
                    DesignTokens.Colors.Border, 1, ButtonBorderStyle.Solid);
            };
        }

        var innerPadding = new Padding(DesignTokens.Spacing.Standard);
        var contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Surface,
            Padding = innerPadding,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        var headerPanel = new FlowLayoutPanel
        {
            Width = cardWidth - innerPadding.Horizontal,
            Height = DesignTokens.ControlHeight.Standard,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = DesignTokens.Colors.Surface,
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Micro)
        };

        var lblOrderNum = new Label
        {
            Text = $"طلب #{order.OrderNumber}",
            Font = DesignTokens.Typography.CardTitle,
            ForeColor = DesignTokens.Colors.TextPrimary,
            AutoSize = true
        };

        var lblElapsed = new Label
        {
            Text = $"{elapsedMinutes} د",
            Font = DesignTokens.Typography.Secondary,
            ForeColor = elapsedMinutes > 20 ? DesignTokens.Colors.Error : DesignTokens.Colors.TextSecondary,
            AutoSize = true,
            Margin = new Padding(DesignTokens.Spacing.Small, 0, 0, 0)
        };

        var lblStationBadge = new Label
        {
            Text = order.Station,
            Font = DesignTokens.Typography.Caption,
            ForeColor = DesignTokens.Colors.Primary,
            BackColor = Color.FromArgb(41, DesignTokens.Colors.Success),
            AutoSize = true,
            Padding = new Padding(DesignTokens.Spacing.Micro, 2, DesignTokens.Spacing.Micro, 2),
            Margin = new Padding(DesignTokens.Spacing.Small, 0, 0, 0)
        };

        headerPanel.Controls.Add(lblOrderNum);
        headerPanel.Controls.Add(lblStationBadge);
        headerPanel.Controls.Add(lblElapsed);

        var lblTableType = new Label
        {
            Text = order.TableOrType,
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.Info,
            Dock = DockStyle.Top,
            Height = 22,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Micro)
        };

        var separator = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = DesignTokens.Colors.Border,
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Small)
        };

        var itemsPanel = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = DesignTokens.Colors.Surface,
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Small)
        };

        int itemY = 0;
        foreach (var item in order.Items)
        {
            var itemPanel = new Panel
            {
                Width = cardWidth - innerPadding.Horizontal - DesignTokens.Spacing.Compact,
                Height = 38,
                Location = new Point(0, itemY),
                BackColor = DesignTokens.Colors.Background,
                Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Micro),
                Padding = new Padding(DesignTokens.Spacing.Small)
            };

            var lblQty = new Label
            {
                Text = $"×{item.Quantity}",
                Font = DesignTokens.Typography.BodyBold,
                ForeColor = DesignTokens.Colors.Primary,
                AutoSize = true,
                Dock = DockStyle.Left,
                Width = 40,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var lblItemName = new Label
            {
                Text = item.Name,
                Font = DesignTokens.Typography.Body,
                ForeColor = DesignTokens.Colors.TextPrimary,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = true
            };

            itemPanel.Controls.Add(lblItemName);
            itemPanel.Controls.Add(lblQty);
            itemsPanel.Controls.Add(itemPanel);

            if (!string.IsNullOrEmpty(item.ModifierSummary))
            {
                var lblMod = new Label
                {
                    Text = $"  ↳ {item.ModifierSummary}",
                    Font = DesignTokens.Typography.Secondary,
                    ForeColor = DesignTokens.Colors.TextSecondary,
                    Dock = DockStyle.Top,
                    Height = 18,
                    TextAlign = ContentAlignment.MiddleRight,
                    Width = cardWidth - innerPadding.Horizontal - DesignTokens.Spacing.Compact,
                    Location = new Point(0, itemY + 38)
                };
                itemsPanel.Controls.Add(lblMod);
                itemY += 56;
            }
            else
            {
                itemY += 40;
            }
        }
        itemsPanel.Height = itemY + DesignTokens.Spacing.Micro;

        Panel? notesPanel = null;
        if (!string.IsNullOrEmpty(order.Notes))
        {
            notesPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = DesignTokens.Colors.WarningLight,
                Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Small),
                Padding = new Padding(DesignTokens.Spacing.Small)
            };
            var lblNotes = new Label
            {
                Text = $"📝 {order.Notes}",
                Font = DesignTokens.Typography.Body,
                ForeColor = DesignTokens.Colors.Warning,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = true
            };
            notesPanel.Controls.Add(lblNotes);
        }

        var btnReady = new RtlButton
        {
            Text = "✅ جاهز",
            Type = order.IsPriority ? RtlButton.ButtonType.Destructive : RtlButton.ButtonType.Success,
            Width = cardWidth - innerPadding.Horizontal,
            Height = DesignTokens.ControlHeight.Standard,
            Dock = DockStyle.Top
        };
        btnReady.Click += (s, e) =>
        {
            var confirm = RtlDialog.ShowConfirm(
                "تأكيد",
                $"هل الطلب #{order.OrderNumber} جاهز؟",
                "نعم، جاهز",
                "إلغاء"
            );
            if (confirm == DialogResult.OK)
            {
                _readyOrderNumbers.Add(order.OrderNumber);
                ApplyStationFilter();
                OrderMarkedReady?.Invoke(this, order.OrderNumber);
            }
        };

        if (order.IsPriority)
        {
            var priorityBanner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 24,
                BackColor = DesignTokens.Colors.Error,
                Padding = new Padding(DesignTokens.Spacing.Small, 0, DesignTokens.Spacing.Small, 0)
            };
            var lblPriority = new Label
            {
                Text = "⚠ عاجل",
                Font = DesignTokens.Typography.BodyBold,
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            priorityBanner.Controls.Add(lblPriority);
            contentPanel.Controls.Add(priorityBanner);
        }

        contentPanel.Controls.Add(btnReady);
        if (notesPanel != null) contentPanel.Controls.Add(notesPanel);
        contentPanel.Controls.Add(itemsPanel);
        contentPanel.Controls.Add(separator);
        contentPanel.Controls.Add(lblTableType);
        contentPanel.Controls.Add(headerPanel);

        card.Controls.Add(contentPanel);
        card.Height = contentPanel.Height + (order.IsPriority ? 6 : 2);

        return card;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
    }

    public new void Dispose()
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        base.Dispose();
    }
}
