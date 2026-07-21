using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using POS.Desktop.Forms;
using POS.Desktop.Themes;
using POS.Desktop.Icons;
using POS.Application.DTOs;
using POS.Application.Services;
// IAuthService is imported via POS.Application.Services (already in scope)
// INotificationService is fully qualified to avoid ambiguity
using NotifSvc = POS.Domain.Interfaces.INotificationService;
using NotifMsg = POS.Domain.Interfaces.NotificationMessage;
using NotifType = POS.Domain.Interfaces.NotificationType;
using NotifCat = POS.Domain.Interfaces.NotificationCategory;

namespace POS.Desktop.Navigation;

/// <summary>
/// Main application shell using DevExpress XtraForm with panel-based navigation sidebar.
/// RTL layout, DevExpress skin support, and centralized navigation events.
/// Includes notification system with bell icon, unread badge, toast popups, and notification center.
/// Icons use Font Awesome (separate Label controls) and text uses Cairo/Segoe UI Arabic font.
/// </summary>
public class MainShell : XtraForm
{
    private PanelControl _sidebar = null!;
    private FlowLayoutPanel _navPanel = null!;
    private PanelControl _contentArea = null!;
    private PanelControl _topBar = null!;
    private LabelControl _lblCurrentUser = null!;
    private LabelControl _lblShift = null!;
    private LabelControl _lblDateTime = null!;
    private SimpleButton _btnLock = null!;
    private SimpleButton _btnLogout = null!;
    private System.Windows.Forms.Timer _clockTimer = null!;
    private DefaultLookAndFeel _defaultLookAndFeel = null!;

    // Notification components
    private readonly NotifSvc _notificationService;
    private Label _btnNotificationBell = null!;
    private Label _lblNotificationBadge = null!;
    private Form _notificationPopup = null!;
    private FlowLayoutPanel _notificationListPanel = null!;

    private static readonly Font _arabicFont = FontLoader.GetArabicFont(10f);
    private static readonly Font _iconFont = FontLoader.GetFontAwesomeSolid(14f);
    private static readonly Font _smallIconFont = FontLoader.GetFontAwesomeSolid(12f);
    private static readonly Font _headerFont = FontLoader.GetArabicFont(12f, FontStyle.Bold);
    private static readonly Color _hoverBack = Color.FromArgb(240, 240, 245);

    // ========================================================================
    // Nav Item Metadata (permission-aware)
    // ========================================================================
    private record NavItemDef(string Label, string Icon, EventHandler Handler, string? RequiredPermission);

    private readonly NavItemDef[] _navItemDefs;

    // ========================================================================
    // Permission State
    // ========================================================================
    private Guid _currentUserId;
    private string _currentRole = string.Empty;
    private readonly Dictionary<string, Panel> _navPanelsByPermission = new();
    private Timer? _notificationTimer;

    // ========================================================================
    // Nav Item Event Handlers (static, referenced via reflection-like pattern)
    // ========================================================================

    public PanelControl ContentArea => _contentArea;

    public event EventHandler? OnNavigateToPOS;
    public event EventHandler? OnNavigateToProducts;
    public event EventHandler? OnNavigateToInventory;
    public event EventHandler? OnNavigateToReports;
    public event EventHandler? OnNavigateToSettings;
    public event EventHandler? OnNavigateToUsers;
    public event EventHandler? OnNavigateToTables;
    public event EventHandler? OnNavigateToPrinters;
    public event EventHandler? OnNavigateToAudit;
    public event EventHandler? OnNavigateToBackup;
    public event EventHandler? OnNavigateToDashboard;
    public event EventHandler? OnNavigateToPromotions;
    public event EventHandler? OnLogout;
    public event EventHandler? OnLock;

    public MainShell() : this(GetDefaultNotificationService())
    {
    }

    private static NotifSvc GetDefaultNotificationService()
    {
        return (AppServiceProvider.Provider?.GetService(typeof(NotifSvc)) as NotifSvc)
               ?? new Services.NotificationService();
    }

    public MainShell(NotifSvc notificationService)
    {
        _notificationService = notificationService;

        // Initialize nav item definitions (must be before InitializeNavSidebar)
        _navItemDefs = new[]
        {
            new NavItemDef("لوحة التحكم", FontAwesomeIcons.Dashboard, (s, e) => OnNavigateToDashboard?.Invoke(this, e), "ViewDashboard"),
            new NavItemDef("نقاط البيع", FontAwesomeIcons.PosTerminal, (s, e) => OnNavigateToPOS?.Invoke(this, e), "Sell"),
            new NavItemDef("المنتجات", FontAwesomeIcons.Products, (s, e) => OnNavigateToProducts?.Invoke(this, e), "EditProducts"),
            new NavItemDef("المخزون", FontAwesomeIcons.Inventory, (s, e) => OnNavigateToInventory?.Invoke(this, e), "AdjustInventory"),
            new NavItemDef("الطاولات", FontAwesomeIcons.Table, (s, e) => OnNavigateToTables?.Invoke(this, e), "ManageTables"),
            new NavItemDef("التقارير", FontAwesomeIcons.Report, (s, e) => OnNavigateToReports?.Invoke(this, e), "ViewReports"),
            new NavItemDef("المستخدمين", FontAwesomeIcons.Users, (s, e) => OnNavigateToUsers?.Invoke(this, e), "ManageUsers"),
            new NavItemDef("الطابعات", FontAwesomeIcons.Printer, (s, e) => OnNavigateToPrinters?.Invoke(this, e), "ManagePrinters"),
            new NavItemDef("العروض الترويجية", FontAwesomeIcons.Discount, (s, e) => OnNavigateToPromotions?.Invoke(this, e), "ManagePromotions"),
            new NavItemDef("سجل المراجعة", FontAwesomeIcons.History, (s, e) => OnNavigateToAudit?.Invoke(this, e), "ViewAuditLog"),
            new NavItemDef("النسخ الاحتياطي", FontAwesomeIcons.Backup, (s, e) => OnNavigateToBackup?.Invoke(this, e), "Backup"),
            new NavItemDef("الإعدادات", FontAwesomeIcons.Settings, (s, e) => OnNavigateToSettings?.Invoke(this, e), "ChangeSettings"),
        };

        InitializeForm();
        InitializeTopBar();
        InitializeNavSidebar();
        InitializeContentArea();
        InitializeClock();
        InitializeNotifications();
        ApplyTheme();
    }

    private void InitializeForm()
    {
        WindowState = FormWindowState.Maximized;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = _arabicFont;
        Text = "نظام نقاط البيع";
        FormBorderEffect = DevExpress.XtraEditors.FormBorderEffect.Shadow;
    }

    private void InitializeTopBar()
    {
        _topBar = new PanelControl
        {
            Dock = DockStyle.Top,
            Height = 52,
            RightToLeft = RightToLeft.Yes
        };

        _lblCurrentUser = new LabelControl
        {
            Text = "المستخدم: المدير",
            Font = _arabicFont,
            ForeColor = Colors.TextSecondary,
            Width = 200,
            Appearance = { TextOptions = { HAlignment = DevExpress.Utils.HorzAlignment.Far } },
            Dock = DockStyle.Right
        };

        _lblShift = new LabelControl
        {
            Text = "الوردية: #1",
            Font = _arabicFont,
            ForeColor = Colors.TextSecondary,
            Width = 150,
            Appearance = { TextOptions = { HAlignment = DevExpress.Utils.HorzAlignment.Far } },
            Dock = DockStyle.Right
        };

        _lblDateTime = new LabelControl
        {
            Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
            Font = _arabicFont,
            ForeColor = Colors.TextSecondary,
            Width = 200,
            Dock = DockStyle.Left
        };

        // Notification bell button with badge
        _btnNotificationBell = new Label
        {
            Text = FontAwesomeIcons.Notification, // fa-bell
            Font = _iconFont,
            ForeColor = Colors.TextPrimary,
            Size = new Size(40, 36),
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        _btnNotificationBell.Click += ToggleNotificationPopup;
        _btnNotificationBell.MouseEnter += (s, e) => _btnNotificationBell.BackColor = _hoverBack;
        _btnNotificationBell.MouseLeave += (s, e) => _btnNotificationBell.BackColor = Color.Transparent;

        // Unread badge overlay on the bell
        _lblNotificationBadge = new Label
        {
            Text = "0",
            Font = new Font("Segoe UI", 7f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = DesignTokens.ErrorColor,
            Size = new Size(16, 16),
            Location = new Point(24, 2),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false,
            Cursor = Cursors.Hand
        };
        // Make badge circular (set region once)
        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            path.AddEllipse(0, 0, _lblNotificationBadge.Width - 1, _lblNotificationBadge.Height - 1);
            _lblNotificationBadge.Region = new Region(path);
        }
        _lblNotificationBadge.Click += ToggleNotificationPopup;

        _btnLogout = new SimpleButton
        {
            Text = "خروج",
            Width = 80,
            Height = 36,
            Dock = DockStyle.Left,
            Font = _arabicFont,
            Appearance = { BackColor = Colors.Surface, ForeColor = Colors.TextPrimary }
        };
        _btnLogout.Click += async (s, e) => await CheckLogoutPermissionAsync();

        _btnLock = new SimpleButton
        {
            Text = "قفل",
            Width = 70,
            Height = 36,
            Dock = DockStyle.Left,
            Font = _arabicFont,
            Margin = new Padding(DesignTokens.Spacing.Small, 0, 0, 0)
        };
        _btnLock.Click += async (s, e) => await CheckLockPermissionAsync();

        // Wrap bell + badge in a container for proper layout
        var bellContainer = new Panel
        {
            Width = 40,
            Height = 36,
            Dock = DockStyle.Left,
            BackColor = Color.Transparent
        };
        bellContainer.Controls.Add(_lblNotificationBadge);
        bellContainer.Controls.Add(_btnNotificationBell);

        _topBar.Controls.Add(_lblCurrentUser);
        _topBar.Controls.Add(_lblShift);
        _topBar.Controls.Add(bellContainer);
        _topBar.Controls.Add(_lblDateTime);
        _topBar.Controls.Add(_btnLogout);
        _topBar.Controls.Add(_btnLock);

        Controls.Add(_topBar);
    }

    // ========================================================================
    // Notification System
    // ========================================================================

    private void InitializeNotifications()
    {
        // Subscribe to notification service events
        _notificationService.NotificationRaised += OnNotificationRaised;

        // Periodic badge update timer (every 2 seconds)
        _notificationTimer = new Timer { Interval = 2000 };
        _notificationTimer.Tick += (s, e) => UpdateNotificationBadge();
        _notificationTimer.Start();

        // Update badge on form load
        UpdateNotificationBadge();
    }

    private void OnNotificationRaised(object? sender, NotifMsg notification)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => OnNotificationRaised(sender, notification)));
            return;
        }

        // Show toast popup
        var toast = new ToastNotificationForm(notification);
        toast.Show(this);

        // Update badge count
        UpdateNotificationBadge();
    }

    private void UpdateNotificationBadge()
    {
        var count = _notificationService.UnreadCount;
        _lblNotificationBadge.Text = count > 99 ? "99+" : count.ToString();
        _lblNotificationBadge.Visible = count > 0;
    }

    private void ToggleNotificationPopup(object? sender, EventArgs e)
    {
        if (_notificationPopup != null && _notificationPopup.Visible)
        {
            CloseNotificationPopup();
        }
        else
        {
            ShowNotificationPopup();
        }
    }

    private void ShowNotificationPopup()
    {
        if (_notificationPopup == null || _notificationPopup.IsDisposed)
        {
            _notificationPopup = new Form
            {
                Width = 380,
                Height = 400,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ControlBox = false,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                BackColor = DesignTokens.SurfaceColor,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                Text = "الإشعارات"
            };

            // Header
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = DesignTokens.PrimaryColor,
                Padding = new Padding(8, 0, 8, 0)
            };

            var headerTitle = new Label
            {
                Text = "الإشعارات",
                Font = new Font(_arabicFont.FontFamily, 11f, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Width = 200,
                Height = 40
            };

            var btnMarkAllRead = new Button
            {
                Text = "تحديد الكل كمقروء",
                Font = new Font(_arabicFont.FontFamily, 8f),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(255, 255, 255, 40),
                Size = new Size(120, 28),
                Dock = DockStyle.Left,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(4, 0, 0, 0)
            };
            btnMarkAllRead.FlatAppearance.BorderSize = 0;
            btnMarkAllRead.Click += (s, e) =>
            {
                _notificationService.MarkAllAsRead();
                RefreshNotificationList();
                UpdateNotificationBadge();
            };

            headerPanel.Controls.Add(headerTitle);
            headerPanel.Controls.Add(btnMarkAllRead);

            // Notification list (scrollable)
            _notificationListPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(4),
                BackColor = DesignTokens.BackgroundColor
            };

            _notificationPopup.Controls.Add(_notificationListPanel);
            _notificationPopup.Controls.Add(headerPanel);
        }

        RefreshNotificationList();

        // Position below the bell icon
        var bellScreenPos = _btnNotificationBell.PointToScreen(Point.Empty);
        _notificationPopup.Location = new Point(
            bellScreenPos.X + _btnNotificationBell.Width - _notificationPopup.Width,
            bellScreenPos.Y + _btnNotificationBell.Height + 2);
        _notificationPopup.Show(this);
        _notificationPopup.BringToFront();
    }

    private void RefreshNotificationList()
    {
        if (_notificationListPanel == null) return;

        _notificationListPanel.Controls.Clear();

        var notifications = _notificationService.Notifications
            .Where(n => !n.IsDismissed)
            .OrderByDescending(n => n.Timestamp)
            .Take(50)
            .ToList();

        if (notifications.Count == 0)
        {
            _notificationListPanel.Controls.Add(new Label
            {
                Text = "لا توجد إشعارات",
                Font = DesignTokens.DefaultFont,
                ForeColor = DesignTokens.TextSecondaryColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Width = 370,
                Height = 60,
                Padding = new Padding(10)
            });
            return;
        }

        foreach (var notif in notifications)
        {
            var item = CreateNotificationItem(notif);
            _notificationListPanel.Controls.Add(item);
        }

        // "View all" link
        var viewAllBtn = new Button
        {
            Text = "عرض الكل...",
            Font = DesignTokens.SmallFont,
            FlatStyle = FlatStyle.Flat,
            ForeColor = DesignTokens.PrimaryColor,
            BackColor = Color.Transparent,
            Width = 370,
            Height = 28,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter
        };
        viewAllBtn.FlatAppearance.BorderSize = 0;
        viewAllBtn.Click += (s, e) =>
        {
            CloseNotificationPopup();
            OnNavigateToAudit?.Invoke(this, EventArgs.Empty);
        };
        _notificationListPanel.Controls.Add(viewAllBtn);
    }

    private Panel CreateNotificationItem(NotifMsg notification)
    {
        var panel = new Panel
        {
            Width = 368,
            Height = 64,
            BackColor = notification.IsRead ? DesignTokens.BackgroundColor : Color.FromArgb(240, 248, 255),
            Margin = new Padding(0, 0, 0, 2),
            Padding = new Padding(8, 4, 8, 4),
            Cursor = Cursors.Hand
        };

        var iconChar = notification.Type switch
        {
            NotifType.Success => "✅",
            NotifType.Warning => "⚠️",
            NotifType.Error => "❌",
            _ => "ℹ️"
        };

        var iconLabel = new Label
        {
            Text = iconChar,
            Font = new Font("Segoe UI Emoji", 14f),
            Location = new Point(340, 8),
            Size = new Size(24, 24),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var titleLabel = new Label
        {
            Text = notification.Title,
            Font = new Font(_arabicFont.FontFamily, 9f, FontStyle.Bold),
            ForeColor = DesignTokens.TextPrimaryColor,
            Location = new Point(40, 6),
            Size = new Size(290, 18),
            TextAlign = ContentAlignment.MiddleRight
        };

        var messageLabel = new Label
        {
            Text = notification.Message,
            Font = new Font(_arabicFont.FontFamily, 8f),
            ForeColor = DesignTokens.TextSecondaryColor,
            Location = new Point(40, 26),
            Size = new Size(290, 18),
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true
        };

        var timeLabel = new Label
        {
            Text = notification.Timestamp.ToString("HH:mm"),
            Font = new Font(_arabicFont.FontFamily, 7f),
            ForeColor = DesignTokens.TextHintColor,
            Location = new Point(40, 44),
            Size = new Size(100, 14),
            TextAlign = ContentAlignment.MiddleRight
        };

        // Unread indicator dot
        if (!notification.IsRead)
        {
            var dot = new Label
            {
                Text = "●",
                Font = new Font("Segoe UI", 6f),
                ForeColor = DesignTokens.PrimaryColor,
                Location = new Point(12, 10),
                Size = new Size(12, 12),
                TextAlign = ContentAlignment.MiddleCenter
            };
            panel.Controls.Add(dot);
        }

        // Hover effect
        panel.MouseEnter += (s, e) => panel.BackColor = _hoverBack;
        panel.MouseLeave += (s, e) => panel.BackColor = notification.IsRead ? DesignTokens.BackgroundColor : Color.FromArgb(240, 248, 255);

        // Click to mark as read and dismiss
        panel.Click += (s, e) =>
        {
            _notificationService.MarkAsRead(notification.Id);
            _notificationService.Dismiss(notification.Id);
            notification.OnClick?.Invoke();
            RefreshNotificationList();
            UpdateNotificationBadge();
        };

        panel.Controls.Add(timeLabel);
        panel.Controls.Add(messageLabel);
        panel.Controls.Add(titleLabel);
        panel.Controls.Add(iconLabel);

        return panel;
    }

    private void CloseNotificationPopup()
    {
        if (_notificationPopup?.IsDisposed == false)
            _notificationPopup.Hide();
    }

    /// <summary>
    /// Shows a notification from anywhere using the global notification service.
    /// Convenience method for forms that don't have direct access to INotificationService.
    /// </summary>
    public static void Notify(string title, string message,
        NotifType type = NotifType.Info,
        NotifCat category = NotifCat.General)
    {
        var service = AppServiceProvider.Provider?.GetService(typeof(NotifSvc)) as NotifSvc;
        if (service == null) return;

        switch (type)
        {
            case NotifType.Success:
                service.ShowSuccess(title, message, category);
                break;
            case NotifType.Warning:
                service.ShowWarning(title, message, category);
                break;
            case NotifType.Error:
                service.ShowError(title, message, category);
                break;
            default:
                service.ShowInfo(title, message, category);
                break;
        }
    }

    private void InitializeNavSidebar()
    {
        _sidebar = new PanelControl
        {
            Dock = DockStyle.Right,
            Width = 240,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(DesignTokens.Spacing.Compact, DesignTokens.Spacing.Standard, DesignTokens.Spacing.Compact, DesignTokens.Spacing.Compact)
        };

        var headerLabel = new LabelControl
        {
            Text = "القائمة الرئيسية",
            Font = _headerFont,
            ForeColor = Colors.TextPrimary,
            Dock = DockStyle.Top,
            Height = 36
        };

        _navPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Padding = new Padding(0)
        };

        foreach (var def in _navItemDefs)
        {
            var item = CreateNavItem(def.Label, def.Icon, def.Handler);
            _navPanel.Controls.Add(item);

            // Store panel reference by permission key for later show/hide
            if (def.RequiredPermission != null)
            {
                _navPanelsByPermission[def.RequiredPermission] = item;
            }
        }

        _sidebar.Controls.Add(_navPanel);
        _sidebar.Controls.Add(headerLabel);
        Controls.Add(_sidebar);
    }

    /// <summary>
    /// Creates a navigation item Panel with separate icon (Font Awesome) and text (Arabic) controls
    /// to avoid WinForms mixed-font rendering issues.
    /// </summary>
    private Panel CreateNavItem(string arabicText, string iconChar, EventHandler? clickHandler)
    {
        var panel = new Panel
        {
            Width = 220,
            Height = 44,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Micro),
            Cursor = Cursors.Hand
        };

        // Font Awesome icon label (right-aligned for RTL)
        var iconLabel = new Label
        {
            Text = iconChar,
            Font = _iconFont,
            ForeColor = Colors.TextPrimary,
            Location = new Point(190, 10),
            Size = new Size(24, 24),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };

        // Arabic text label (to the left of the icon)
        var textLabel = new Label
        {
            Text = arabicText,
            Font = _arabicFont,
            ForeColor = Colors.TextPrimary,
            Location = new Point(30, 10),
            Size = new Size(155, 24),
            TextAlign = ContentAlignment.MiddleRight,
            BackColor = Color.Transparent,
            RightToLeft = RightToLeft.Yes
        };

        // Hover effects
        panel.MouseEnter += (s, e) => panel.BackColor = _hoverBack;
        panel.MouseLeave += (s, e) => panel.BackColor = Color.Transparent;
        iconLabel.MouseEnter += (s, e) => panel.BackColor = _hoverBack;
        iconLabel.MouseLeave += (s, e) => panel.BackColor = Color.Transparent;
        textLabel.MouseEnter += (s, e) => panel.BackColor = _hoverBack;
        textLabel.MouseLeave += (s, e) => panel.BackColor = Color.Transparent;

        // Click handler on both the panel and child controls
        panel.Click += (s, e) => clickHandler?.Invoke(this, e);
        iconLabel.Click += (s, e) => clickHandler?.Invoke(this, e);
        textLabel.Click += (s, e) => clickHandler?.Invoke(this, e);

        panel.Controls.Add(textLabel);
        panel.Controls.Add(iconLabel);

        return panel;
    }

    private void InitializeContentArea()
    {
        _contentArea = new PanelControl
        {
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(DesignTokens.Spacing.Standard)
        };
        Controls.Add(_contentArea);
    }

    private void InitializeClock()
    {
        _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _clockTimer.Tick += (s, e) =>
        {
            _lblDateTime.Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
        };
        _clockTimer.Start();
    }

    private void ApplyTheme()
    {
        try
        {
            DevExpress.Skins.SkinManager.EnableFormSkins();
            try { DevExpress.UserSkins.BonusSkins.Register(); } catch { }
            _defaultLookAndFeel = new DefaultLookAndFeel();
            _defaultLookAndFeel.LookAndFeel.SkinName = "Office 2022 Colorful";
        }
        catch
        {
            _defaultLookAndFeel = new DefaultLookAndFeel();
            _defaultLookAndFeel.LookAndFeel.Style = LookAndFeelStyle.UltraFlat;
        }
    }

    public void NavigateTo(UserControl control)
    {
        _contentArea.Controls.Clear();
        control.Dock = DockStyle.Fill;
        _contentArea.Controls.Add(control);
    }

    /// <summary>
    /// Creates a POS terminal form, wires its payment/retrieve events to the appropriate dialogs,
    /// and navigates to it.
    /// </summary>
    public void NavigateToPOS(IServiceProvider serviceProvider)
    {
        var posTerminal = serviceProvider.GetRequiredService<PosTerminalForm>();

        // Wire payment request → show PaymentDialog
        posTerminal.RequestPayment += (sender, paymentRequest) =>
        {
            if (sender is not PosTerminalForm pos) return;

            var saleService = serviceProvider.GetService<POS.Application.Services.ISaleService>();
            using var paymentDialog = saleService != null
                ? new PaymentDialog(paymentRequest.Amount, paymentRequest.SaleId, saleService)
                : new PaymentDialog(paymentRequest.Amount, paymentRequest.SaleId);

            paymentDialog.PaymentSucceeded += (ps, pe) =>
            {
                // Read the change amount from PaymentDialog's public property
                var change = paymentDialog.ChangeAmount;
                pos.OnPaymentSuccess(change);

                // Send notification
                _notificationService.ShowSuccess(
                    "تمت عملية الدفع بنجاح",
                    $"تم دفع {paymentRequest.Amount:N3} JOD بنجاح",
                    NotifCat.Sale);
            };

            paymentDialog.PaymentCancelled += (ps, pe) =>
            {
                // Return to active sale state
            };

            paymentDialog.ShowDialog(this);
        };

        // Wire hold request
        posTerminal.RequestHold += (sender, e) =>
        {
            _notificationService.ShowInfo(
                "تم تعليق الفاتورة",
                "تم تعليق الفاتورة بنجاح",
                NotifCat.Sale);
        };

        // Wire retrieve request
        posTerminal.RequestRetrieve += (sender, e) =>
        {
            _notificationService.ShowInfo(
                "تم استرجاع الفاتورة",
                "تم استرجاع الفاتورة المعلقة بنجاح",
                NotifCat.Sale);
        };

        // Load products and categories
        _ = posTerminal.LoadCategoriesAsync();
        _ = posTerminal.LoadProductsAsync();
        posTerminal.InitializeBarcodeScanner();

        NavigateTo(posTerminal);
    }

    /// <summary>
    /// Checks ChangeSettings permission before allowing logout.
    /// Only users with settings management permission can log out.
    /// </summary>
    private async Task CheckLogoutPermissionAsync()
    {
        if (_currentUserId == Guid.Empty)
        {
            OnLogout?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (AppServiceProvider.Provider == null)
        {
            OnLogout?.Invoke(this, EventArgs.Empty);
            return;
        }

        var authService = AppServiceProvider.Provider.GetService(typeof(IAuthService)) as IAuthService;
        if (authService == null)
        {
            OnLogout?.Invoke(this, EventArgs.Empty);
            return;
        }

        try
        {
            var hasPermission = await authService.HasPermissionAsync(_currentUserId, "ChangeSettings");
            if (hasPermission)
            {
                OnLogout?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                MessageBox.Show(
                    "ليس لديك صلاحية تسجيل الخروج. يرجى التواصل مع مدير النظام.",
                    "صلاحية مقفلة",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            }
        }
        catch
        {
            OnLogout?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Stores the authenticated user context and triggers an async permission check
    /// to show/hide nav items based on their role permissions.
    /// </summary>
    public void SetUserContext(Guid userId, string userName, string role)
    {
        _currentUserId = userId;
        _currentRole = role;
        _lblCurrentUser.Text = $"{userName} :المستخدم";

        // Fire-and-forget the permission check so the UI stays responsive
        _ = ApplyPermissionsAsync();
    }

    /// <summary>
    /// Updates shift display info.
    /// </summary>
    public void UpdateShiftInfo(string shiftInfo)
    {
        _lblShift.Text = shiftInfo;
    }

    /// <summary>
    /// Checks ChangeSettings permission before allowing lock.
    /// Only users with settings management permission can lock the system.
    /// </summary>
    private async Task CheckLockPermissionAsync()
    {
        if (_currentUserId == Guid.Empty)
        {
            OnLock?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (AppServiceProvider.Provider == null)
        {
            OnLock?.Invoke(this, EventArgs.Empty);
            return;
        }

        var authService = AppServiceProvider.Provider.GetService(typeof(IAuthService)) as IAuthService;
        if (authService == null)
        {
            OnLock?.Invoke(this, EventArgs.Empty);
            return;
        }

        try
        {
            var hasPermission = await authService.HasPermissionAsync(_currentUserId, "ChangeSettings");
            if (hasPermission)
            {
                OnLock?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                MessageBox.Show(
                    "ليس لديك صلاحية قفل النظام. يرجى التواصل مع مدير النظام.",
                    "صلاحية مقفلة",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            }
        }
        catch
        {
            // On error, allow lock as safe default
            OnLock?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Updates user display info (kept for backwards compatibility — delegates to SetUserContext).
    /// </summary>
    public void UpdateUserInfo(string userName, string shiftInfo)
    {
        _lblCurrentUser.Text = $"{userName} :المستخدم";
        _lblShift.Text = shiftInfo;
    }

    /// <summary>
    /// Asynchronously checks permissions and hides nav items the user lacks access to.
    /// Items with no RequiredPermission are always visible.
    /// If no user context is set or AuthService is unavailable, all items remain visible.
    /// </summary>
    public async Task ApplyPermissionsAsync()
    {
        if (_currentUserId == Guid.Empty)
            return;

        if (AppServiceProvider.Provider == null)
            return;

        var authService = AppServiceProvider.Provider.GetService(typeof(IAuthService)) as IAuthService;
        if (authService == null)
            return;

        foreach (var def in _navItemDefs)
        {
            var permission = def.RequiredPermission;
            if (permission == null)
                continue; // Always visible

            if (!_navPanelsByPermission.TryGetValue(permission, out var panel))
                continue;

            try
            {
                var hasPermission = await authService.HasPermissionAsync(_currentUserId, permission);
                panel.Visible = hasPermission;
            }
            catch
            {
                // On error (e.g. DB unavailable), keep item visible as safe default
                panel.Visible = true;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clockTimer?.Stop();
            _clockTimer?.Dispose();
            _notificationTimer?.Stop();
            _notificationTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
