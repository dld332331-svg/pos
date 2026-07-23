using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using POS.Desktop.CustomControls;
using POS.Desktop.Forms;
using POS.Desktop.Themes;
using POS.Desktop.Icons;
using POS.Application.DTOs;
using POS.Application.Services;
using NotifSvc = POS.Domain.Interfaces.INotificationService;
using NotifMsg = POS.Domain.Interfaces.NotificationMessage;
using NotifType = POS.Domain.Interfaces.NotificationType;
using NotifCat = POS.Domain.Interfaces.NotificationCategory;

namespace POS.Desktop.Navigation;

/// <summary>
/// MainShell — POS_EN.md §12 Application Shell.
/// Modern RTL shell with glass-morphic sidebar, top bar, notification bell, live clock,
/// permission-aware navigation, and professional visual hierarchy.
/// </summary>
public class MainShell : XtraForm
{
    // ── Layout Constants ──
    private const int SidebarWidth = 260;
    private const int TopBarHeight = 60;
    private const int NavItemHeight = 48;
    private const int SidebarIconSize = 20;

    // ── Fonts ──
    private static readonly Font _arabicFont = FontLoader.GetArabicFont(10.5f);
    private static readonly Font _arabicBold = FontLoader.GetArabicFont(10.5f, FontStyle.Bold);
    private static readonly Font _arabicHeader = FontLoader.GetArabicFont(13f, FontStyle.Bold);
    private static readonly Font _iconFont = FontLoader.GetFontAwesomeSolid(14f);
    private static readonly Font _smallIconFont = FontLoader.GetFontAwesomeSolid(11f);
    private static readonly Font _bellIconFont = FontLoader.GetFontAwesomeSolid(18f);
    private static readonly Font _badgeFont = new Font("Segoe UI", 7f, FontStyle.Bold);

    // ── Colors ──
    private static readonly Color SidebarBg = DesignTokens.Colors.SidebarBackground;
    private static readonly Color SidebarHover = DesignTokens.Colors.SidebarHover;
    private static readonly Color SidebarActive = DesignTokens.Colors.SidebarActive;
    private static readonly Color SidebarText = DesignTokens.Colors.SidebarText;
    private static readonly Color SidebarTextActive = DesignTokens.Colors.SidebarTextActive;
    private static readonly Color TopBarBg = DesignTokens.Colors.Surface;
    private static readonly Color TopBarBorder = DesignTokens.Colors.Border;

    // ── Controls ──
    private Panel _sidebar = null!;
    private FlowLayoutPanel _navPanel = null!;
    private PanelControl _contentArea = null!;
    private Panel _topBar = null!;
    private Label _lblCurrentUser = null!;
    private Label _lblShift = null!;
    private Label _lblDateTime = null!;
    private RtlButton _btnLock = null!;
    private RtlButton _btnLogout = null!;
    private Timer _clockTimer = null!;
    private DefaultLookAndFeel _defaultLookAndFeel = null!;

    // ── Notification Components ──
    private readonly NotifSvc _notificationService;
    private Label _btnNotificationBell = null!;
    private Label _lblNotificationBadge = null!;
    private Form _notificationPopup = null!;
    private FlowLayoutPanel _notificationListPanel = null!;
    private Timer _notificationTimer = null!;

    // ── App Logo ──
    private Panel _appLogoPanel = null!;
    private Label _appLogoText = null!;
    private Label _appLogoSubtext = null!;

    // ── Nav Item Definitions ──
    private record NavItemDef(string Label, string Icon, EventHandler Handler, string? RequiredPermission);
    private readonly NavItemDef[] _navItemDefs;
    private readonly Dictionary<string, Panel> _navPanelsByPermission = new();

    // ── State ──
    private Guid _currentUserId;
    private string _currentRole = string.Empty;
    private Panel? _activeNavItem;

    // ── Events ──
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
    public event EventHandler? OnNavigateToReturns;
    public event EventHandler? OnLogout;
    public event EventHandler? OnLock;

    public MainShell(NotifSvc notificationService)
    {
        _notificationService = notificationService;

        _navItemDefs = new[]
        {
            new NavItemDef("لوحة التحكم", FontAwesomeIcons.Dashboard, (s, e) => OnNavigateToDashboard?.Invoke(this, e), "ViewDashboard"),
            new NavItemDef("نقاط البيع", FontAwesomeIcons.PosTerminal, (s, e) => OnNavigateToPOS?.Invoke(this, e), "Sell"),
            new NavItemDef("المنتجات", FontAwesomeIcons.Products, (s, e) => OnNavigateToProducts?.Invoke(this, e), "EditProducts"),
            new NavItemDef("المخزون", FontAwesomeIcons.Inventory, (s, e) => OnNavigateToInventory?.Invoke(this, e), "AdjustInventory"),
            new NavItemDef("الطاولات", FontAwesomeIcons.Table, (s, e) => OnNavigateToTables?.Invoke(this, e), "ManageTables"),
            new NavItemDef("التقارير", FontAwesomeIcons.Report, (s, e) => OnNavigateToReports?.Invoke(this, e), "ViewReports"),
            new NavItemDef("العروض", FontAwesomeIcons.Discount, (s, e) => OnNavigateToPromotions?.Invoke(this, e), "ManagePromotions"),
            new NavItemDef("المستخدمين", FontAwesomeIcons.Users, (s, e) => OnNavigateToUsers?.Invoke(this, e), "ManageUsers"),
            new NavItemDef("الطابعات", FontAwesomeIcons.Printer, (s, e) => OnNavigateToPrinters?.Invoke(this, e), "ManagePrinters"),
            new NavItemDef("المرتجعات", FontAwesomeIcons.Return, (s, e) => OnNavigateToReturns?.Invoke(this, e), "ReturnItem"),
            new NavItemDef("سجل المراجعة", FontAwesomeIcons.History, (s, e) => OnNavigateToAudit?.Invoke(this, e), "ViewAuditLog"),
            new NavItemDef("النسخ الاحتياطي", FontAwesomeIcons.Backup, (s, e) => OnNavigateToBackup?.Invoke(this, e), "Backup"),
            new NavItemDef("الإعدادات", FontAwesomeIcons.Settings, (s, e) => OnNavigateToSettings?.Invoke(this, e), "ChangeSettings"),
        };

        InitializeForm();
        InitializeTopBar();
        InitializeSidebar();
        InitializeContentArea();
        InitializeNotifications();
        InitializeClock();
        ApplyTheme();
    }

    private void InitializeForm()
    {
        WindowState = FormWindowState.Maximized;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = _arabicFont;
        Text = "نظام نقاط البيع";
        KeyPreview = true;
        BackColor = DesignTokens.Colors.Background;
        KeyDown += MainShell_KeyDown;
    }

    private void MainShell_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_contentArea.Controls.Count == 0) return;
        if (_contentArea.Controls[0] is PosTerminalForm posTerminal)
        {
            posTerminal.HandleKeyDown(e);
        }
    }

    private void InitializeTopBar()
    {
        _topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = TopBarHeight,
            BackColor = TopBarBg
        };

        // Bottom border line
        var borderLine = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = TopBarBorder
        };

        // ── Left Side: Date/Time ──
        _lblDateTime = new Label
        {
            Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
            Font = _arabicFont,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            Width = 180,
            Padding = new Padding(DesignTokens.Spacing.Standard, 0, 0, 0)
        };

        // ── Right Side: User info ──
        _lblCurrentUser = new Label
        {
            Text = "المستخدم: المدير",
            Font = _arabicBold,
            ForeColor = DesignTokens.Colors.TextPrimary,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Width = 190,
            Padding = new Padding(0, 0, DesignTokens.Spacing.Standard, 0)
        };

        _lblShift = new Label
        {
            Text = "الوردية: #1",
            Font = _arabicFont,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Width = 130,
            Padding = new Padding(0, 0, DesignTokens.Spacing.Small, 0)
        };

        // ── Action Buttons ──
        _btnLogout = new RtlButton
        {
            Text = "خروج",
            IconText = FontAwesomeIcons.Logout,
            Type = RtlButton.ButtonType.Ghost,
            Size = new Size(90, TopBarHeight - 12),
            Dock = DockStyle.Left,
            CornerRadius = DesignTokens.Radius.Md
        };
        _btnLogout.Click += async (s, e) => await CheckLogoutPermissionAsync();

        _btnLock = new RtlButton
        {
            Text = "قفل",
            IconText = FontAwesomeIcons.Lock,
            Type = RtlButton.ButtonType.Ghost,
            Size = new Size(80, TopBarHeight - 12),
            Dock = DockStyle.Left,
            CornerRadius = DesignTokens.Radius.Md
        };
        _btnLock.Click += async (s, e) => await CheckLockPermissionAsync();

        // ── Notification Bell ──
        var bellContainer = new Panel
        {
            Width = 48,
            Height = TopBarHeight,
            Dock = DockStyle.Left,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };

        _btnNotificationBell = new Label
        {
            Text = FontAwesomeIcons.Notification,
            Font = _bellIconFont,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        _btnNotificationBell.Click += ToggleNotificationPopup;
        _btnNotificationBell.MouseEnter += (s, e) => bellContainer.BackColor = DesignTokens.Colors.Background;
        _btnNotificationBell.MouseLeave += (s, e) => bellContainer.BackColor = Color.Transparent;

        _lblNotificationBadge = new Label
        {
            Text = "0",
            Font = _badgeFont,
            ForeColor = Color.White,
            BackColor = DesignTokens.Colors.Accent,
            Size = new Size(18, 18),
            Location = new Point(26, 8),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false,
            Cursor = Cursors.Hand
        };
        using (var path = new GraphicsPath())
        {
            path.AddEllipse(0, 0, _lblNotificationBadge.Width - 1, _lblNotificationBadge.Height - 1);
            _lblNotificationBadge.Region = new Region(path);
        }
        _lblNotificationBadge.Click += ToggleNotificationPopup;

        bellContainer.Controls.Add(_lblNotificationBadge);
        bellContainer.Controls.Add(_btnNotificationBell);

        _topBar.Controls.Add(borderLine);
        _topBar.Controls.Add(_lblCurrentUser);
        _topBar.Controls.Add(_lblShift);
        _topBar.Controls.Add(bellContainer);
        _topBar.Controls.Add(_lblDateTime);
        _topBar.Controls.Add(_btnLogout);
        _topBar.Controls.Add(_btnLock);

        Controls.Add(_topBar);
    }

    // ========================================================================
    // Sidebar Navigation
    // ========================================================================

    private void InitializeSidebar()
    {
        _sidebar = new Panel
        {
            Dock = DockStyle.Right,
            Width = SidebarWidth,
            BackColor = SidebarBg
        };

        // ── App Logo Area ──
        _appLogoPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 110,
            BackColor = DesignTokens.Colors.SidebarBackgroundDarker
        };
        _appLogoPanel.Paint += (s, e) =>
        {
            using var path = DesignTokens.CreateRoundedRect(_appLogoPanel.ClientRectangle, 0);
        };

        _appLogoText = new Label
        {
            Text = "POS",
            Font = new Font("Segoe UI", 24f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 22),
            Size = new Size(210, 38),
            TextAlign = ContentAlignment.MiddleRight
        };

        _appLogoSubtext = new Label
        {
            Text = "نظام نقاط البيع",
            Font = _arabicFont,
            ForeColor = SidebarText,
            Location = new Point(20, 62),
            Size = new Size(210, 24),
            TextAlign = ContentAlignment.MiddleRight
        };

        // Version badge
        var versionBadge = new Label
        {
            Text = "v1.0",
            Font = new Font("Segoe UI", 7f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = DesignTokens.Colors.Primary,
            Size = new Size(42, 20),
            Location = new Point(188, 30),
            TextAlign = ContentAlignment.MiddleCenter
        };
        versionBadge.Paint += (s, e) =>
        {
            using var path = DesignTokens.CreateRoundedRect(versionBadge.ClientRectangle, 6);
            versionBadge.Region = new Region(path);
        };

        _appLogoPanel.Controls.Add(versionBadge);
        _appLogoPanel.Controls.Add(_appLogoSubtext);
        _appLogoPanel.Controls.Add(_appLogoText);

        // ── Navigation Items ──
        _navPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Padding = new Padding(DesignTokens.Spacing.Small, DesignTokens.Spacing.Compact, DesignTokens.Spacing.Small, DesignTokens.Spacing.Compact)
        };

        // Separator
        var sep = new Panel
        {
            Width = SidebarWidth - 32,
            Height = 1,
            BackColor = DesignTokens.Colors.SidebarDivider,
            Margin = new Padding(12, 4, 12, 8)
        };
        _navPanel.Controls.Add(sep);

        foreach (var def in _navItemDefs)
        {
            var item = CreateNavItem(def.Label, def.Icon, def.Handler);
            _navPanel.Controls.Add(item);
            if (def.RequiredPermission != null)
                _navPanelsByPermission[def.RequiredPermission] = item;
        }

        // ── Bottom Section: Sidebar footer ──
        var sidebarFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            BackColor = DesignTokens.Colors.SidebarBackgroundDarker
        };

        var footerLabel = new Label
        {
            Text = $"{FontAwesomeIcons.Copyright} 2026",
            Font = _smallIconFont,
            ForeColor = Color.FromArgb(71, 85, 105),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };
        sidebarFooter.Controls.Add(footerLabel);

        _sidebar.Controls.Add(_navPanel);
        _sidebar.Controls.Add(sidebarFooter);
        _sidebar.Controls.Add(_appLogoPanel);

        Controls.Add(_sidebar);
    }

    private Panel CreateNavItem(string arabicText, string iconChar, EventHandler? clickHandler)
    {
        var panel = new Panel
        {
            Width = SidebarWidth - 24,
            Height = NavItemHeight,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 4),
            Cursor = Cursors.Hand
        };
        panel.Paint += (s, e) =>
        {
            if (panel.BackColor != Color.Transparent)
            {
                using var path = DesignTokens.CreateRoundedRect(panel.ClientRectangle, DesignTokens.Radius.Md);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(panel.BackColor);
                e.Graphics.FillPath(brush, path);
            }
        };

        // Icon (right side in RTL layout)
        var iconLabel = new Label
        {
            Text = iconChar,
            Font = _iconFont,
            ForeColor = SidebarText,
            Location = new Point(8, (NavItemHeight - SidebarIconSize) / 2),
            Size = new Size(SidebarIconSize, SidebarIconSize),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };

        // Text (to the left of icon in RTL layout)
        var textLabel = new Label
        {
            Text = arabicText,
            Font = _arabicFont,
            ForeColor = SidebarText,
            Location = new Point(8 + SidebarIconSize + 8, 0),
            Size = new Size(SidebarWidth - 24 - SidebarIconSize - 32, NavItemHeight),
            TextAlign = ContentAlignment.MiddleRight,
            BackColor = Color.Transparent,
            RightToLeft = RightToLeft.Yes
        };

        // Hover + Click
        panel.MouseEnter += (s, e) => { if (panel != _activeNavItem) panel.BackColor = SidebarHover; };
        panel.MouseLeave += (s, e) => { if (panel != _activeNavItem) panel.BackColor = Color.Transparent; };
        iconLabel.MouseEnter += (s, e) => { if (panel != _activeNavItem) panel.BackColor = SidebarHover; };
        iconLabel.MouseLeave += (s, e) => { if (panel != _activeNavItem) panel.BackColor = Color.Transparent; };
        textLabel.MouseEnter += (s, e) => { if (panel != _activeNavItem) panel.BackColor = SidebarHover; };
        textLabel.MouseLeave += (s, e) => { if (panel != _activeNavItem) panel.BackColor = Color.Transparent; };

        void ClickAction(object? s, EventArgs e)
        {
            SetActiveNavItem(panel);
            clickHandler?.Invoke(this, e);
        }

        panel.Click += ClickAction;
        iconLabel.Click += ClickAction;
        textLabel.Click += ClickAction;

        panel.Controls.Add(textLabel);
        panel.Controls.Add(iconLabel);

        return panel;
    }

    private void SetActiveNavItem(Panel? item)
    {
        // Reset previous active
        if (_activeNavItem != null)
        {
            _activeNavItem.BackColor = Color.Transparent;
            foreach (Label lbl in _activeNavItem.Controls.OfType<Label>())
            {
                lbl.ForeColor = SidebarText;
            }
        }

        _activeNavItem = item;

        if (_activeNavItem != null)
        {
            _activeNavItem.BackColor = SidebarActive;
            foreach (Label lbl in _activeNavItem.Controls.OfType<Label>())
            {
                lbl.ForeColor = SidebarTextActive;
            }
        }
    }

    private void InitializeContentArea()
    {
        _contentArea = new PanelControl
        {
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(0),
            BackColor = DesignTokens.Colors.Background
        };
        Controls.Add(_contentArea);
    }

    private void InitializeClock()
    {
        _clockTimer = new Timer { Interval = 1000 };
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
            try { DevExpress.UserSkins.BonusSkins.Register(); } catch { System.Diagnostics.Trace.TraceWarning("[Skins] BonusSkins not available, using default skin"); }
            _defaultLookAndFeel = new DefaultLookAndFeel();
            _defaultLookAndFeel.LookAndFeel.Style = LookAndFeelStyle.UltraFlat;
            _defaultLookAndFeel.LookAndFeel.UseDefaultLookAndFeel = false;
        }
        catch
        {
            _defaultLookAndFeel = new DefaultLookAndFeel();
            _defaultLookAndFeel.LookAndFeel.Style = LookAndFeelStyle.UltraFlat;
        }
    }

    // ========================================================================
    // Notifications
    // ========================================================================

    private void InitializeNotifications()
    {
        _notificationService.NotificationRaised += OnNotificationRaised;
        _notificationTimer = new Timer { Interval = 2000 };
        _notificationTimer.Tick += (s, e) => UpdateNotificationBadge();
        _notificationTimer.Start();
        UpdateNotificationBadge();
    }

    private void OnNotificationRaised(object? sender, NotifMsg notification)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => OnNotificationRaised(sender, notification)));
            return;
        }
        var toast = new ToastNotificationForm(notification);
        toast.Show(this);
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
            CloseNotificationPopup();
        else
            ShowNotificationPopup();
    }

    private void ShowNotificationPopup()
    {
        if (_notificationPopup == null || _notificationPopup.IsDisposed)
        {
            _notificationPopup = new Form
            {
                Width = 400,
                Height = 420,
                FormBorderStyle = FormBorderStyle.None,
                ControlBox = false,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                BackColor = DesignTokens.Colors.Surface,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                Text = "الإشعارات"
            };
            _notificationPopup.Paint += (s, e) =>
            {
                using var path = DesignTokens.CreateRoundedRect(_notificationPopup.ClientRectangle, DesignTokens.Radius.Lg);
                _notificationPopup.Region = new Region(path);
                using var pen = new Pen(DesignTokens.Colors.Border, 1);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, path);
            };

            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = DesignTokens.Colors.Surface,
                Padding = new Padding(14, 0, 14, 0)
            };

            var headerTitle = new Label
            {
                Text = "الإشعارات",
                Font = _arabicHeader,
                ForeColor = DesignTokens.Colors.TextPrimary,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Width = 220,
                Height = 52,
                RightToLeft = RightToLeft.Yes
            };

            var btnMarkAllRead = new RtlButton
            {
                Text = "تحديد الكل كمقروء",
                Type = RtlButton.ButtonType.Ghost,
                Size = new Size(130, 32),
                Dock = DockStyle.Left,
                CornerRadius = DesignTokens.Radius.Md,
                Font = FontLoader.GetArabicFont(9f)
            };
            btnMarkAllRead.Click += (s, e) =>
            {
                _notificationService.MarkAllAsRead();
                RefreshNotificationList();
                UpdateNotificationBadge();
            };

            headerPanel.Controls.Add(headerTitle);
            headerPanel.Controls.Add(btnMarkAllRead);

            _notificationListPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(8),
                BackColor = DesignTokens.Colors.Background
            };

            _notificationPopup.Controls.Add(headerPanel);
            _notificationPopup.Controls.Add(_notificationListPanel);
        }

        RefreshNotificationList();

        var bellScreenPos = _btnNotificationBell.PointToScreen(Point.Empty);
        _notificationPopup.Location = new Point(
            bellScreenPos.X + _btnNotificationBell.Width - _notificationPopup.Width,
            bellScreenPos.Y + _btnNotificationBell.Height + 4);
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
                Font = DesignTokens.Typography.Body,
                ForeColor = DesignTokens.Colors.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                Width = 370,
                Height = 60,
                Padding = new Padding(10),
                RightToLeft = RightToLeft.Yes
            });
            return;
        }

        foreach (var notif in notifications)
        {
            var item = CreateNotificationItem(notif);
            _notificationListPanel.Controls.Add(item);
        }

        var viewAllBtn = new RtlButton
        {
            Text = "عرض الكل...",
            Type = RtlButton.ButtonType.Ghost,
            Size = new Size(370, 32),
            CornerRadius = DesignTokens.Radius.Md,
            Font = FontLoader.GetArabicFont(9.5f)
        };
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
            Width = 370,
            Height = 72,
            BackColor = notification.IsRead ? DesignTokens.Colors.Background : DesignTokens.Colors.InfoSoft,
            Margin = new Padding(0, 0, 0, 4),
            Padding = new Padding(10, 6, 10, 6),
            Cursor = Cursors.Hand
        };
        panel.Paint += (s, e) =>
        {
            using var path = DesignTokens.CreateRoundedRect(panel.ClientRectangle, DesignTokens.Radius.Md);
            panel.Region = new Region(path);
        };

        var iconChar = notification.Type switch
        {
            NotifType.Success => FontAwesomeIcons.Success,
            NotifType.Warning => FontAwesomeIcons.Warning,
            NotifType.Error => FontAwesomeIcons.Error,
            _ => FontAwesomeIcons.Info
        };
        var iconColor = notification.Type switch
        {
            NotifType.Success => DesignTokens.Colors.Success,
            NotifType.Warning => DesignTokens.Colors.Warning,
            NotifType.Error => DesignTokens.Colors.Error,
            _ => DesignTokens.Colors.Info
        };

        var iconLabel = new Label
        {
            Text = iconChar,
            Font = _iconFont,
            ForeColor = iconColor,
            Location = new Point(8, 10),
            Size = new Size(24, 24),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var titleLabel = new Label
        {
            Text = notification.Title,
            Font = _arabicBold,
            ForeColor = DesignTokens.Colors.TextPrimary,
            Location = new Point(40, 4),
            Size = new Size(290, 22),
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes
        };

        var messageLabel = new Label
        {
            Text = notification.Message,
            Font = DesignTokens.Typography.Secondary,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Location = new Point(40, 26),
            Size = new Size(290, 20),
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true,
            RightToLeft = RightToLeft.Yes
        };

        var timeLabel = new Label
        {
            Text = notification.Timestamp.ToString("HH:mm"),
            Font = DesignTokens.Typography.Caption,
            ForeColor = DesignTokens.Colors.TextHint,
            Location = new Point(40, 46),
            Size = new Size(100, 16),
            TextAlign = ContentAlignment.MiddleRight
        };

        if (!notification.IsRead)
        {
            var dot = new Label
            {
                Text = "●",
                Font = new Font("Segoe UI", 6f),
                ForeColor = DesignTokens.Colors.Primary,
                Location = new Point(352, 12),
                Size = new Size(12, 12),
                TextAlign = ContentAlignment.MiddleCenter
            };
            panel.Controls.Add(dot);
        }

        panel.MouseEnter += (s, e) => panel.BackColor = DesignTokens.Colors.PrimaryLighter;
        panel.MouseLeave += (s, e) => panel.BackColor = notification.IsRead ? DesignTokens.Colors.Background : DesignTokens.Colors.InfoSoft;

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

    public static void Notify(string title, string message,
        NotifType type = NotifType.Info, NotifCat category = NotifCat.General)
    {
        var service = AppServiceProvider.Provider?.GetService(typeof(NotifSvc)) as NotifSvc;
        if (service == null) return;

        switch (type)
        {
            case NotifType.Success: service.ShowSuccess(title, message, category); break;
            case NotifType.Warning: service.ShowWarning(title, message, category); break;
            case NotifType.Error: service.ShowError(title, message, category); break;
            default: service.ShowInfo(title, message, category); break;
        }
    }

    // ========================================================================
    // Navigation
    // ========================================================================

    public void NavigateTo(UserControl control)
    {
        _contentArea.Controls.Clear();
        control.Dock = DockStyle.Fill;
        _contentArea.Controls.Add(control);
    }

    public void NavigateToPOS(IServiceProvider serviceProvider)
    {
        var posTerminal = serviceProvider.GetRequiredService<PosTerminalForm>();

        posTerminal.RequestPayment += (sender, paymentRequest) =>
        {
            if (sender is not PosTerminalForm pos) return;
            var saleService = serviceProvider.GetService<POS.Application.Services.ISaleService>();
            using var paymentDialog = saleService != null
                ? new PaymentDialog(paymentRequest.Amount, paymentRequest.SaleId, saleService)
                : new PaymentDialog(paymentRequest.Amount, paymentRequest.SaleId);

            paymentDialog.PaymentSucceeded += (ps, pe) =>
            {
                var change = paymentDialog.ChangeAmount;
                pos.OnPaymentSuccess(change);
                _notificationService.ShowSuccess("تمت عملية الدفع بنجاح",
                    $"تم دفع {paymentRequest.Amount:N3} JOD بنجاح", NotifCat.Sale);
            };
            paymentDialog.PaymentCancelled += (ps, pe) => { };
            paymentDialog.ShowDialog(this);
        };

        posTerminal.RequestHold += (sender, e) =>
        {
            _notificationService.ShowInfo("تم تعليق الفاتورة", "تم تعليق الفاتورة بنجاح", NotifCat.Sale);
        };

        posTerminal.RequestRetrieve += (sender, e) =>
        {
            _notificationService.ShowInfo("تم استرجاع الفاتورة", "تم استرجاع الفاتورة المعلقة بنجاح", NotifCat.Sale);
        };

        _ = posTerminal.LoadCategoriesAsync();
        _ = posTerminal.LoadProductsAsync();
        posTerminal.InitializeBarcodeScanner();
        NavigateTo(posTerminal);
    }

    // ========================================================================
    // Permission Checks
    // ========================================================================

    private async Task CheckLogoutPermissionAsync()
    {
        if (_currentUserId == Guid.Empty)
        { OnLogout?.Invoke(this, EventArgs.Empty); return; }

        if (AppServiceProvider.Provider == null)
        { OnLogout?.Invoke(this, EventArgs.Empty); return; }

        using var scope = AppServiceProvider.Provider.CreateScope();
        var authService = scope.ServiceProvider.GetService(typeof(IAuthService)) as IAuthService;
        if (authService == null) { OnLogout?.Invoke(this, EventArgs.Empty); return; }

        try
        {
            if (await authService.HasPermissionAsync(_currentUserId, "ChangeSettings"))
                OnLogout?.Invoke(this, EventArgs.Empty);
            else
                MessageBox.Show("ليس لديك صلاحية تسجيل الخروج. يرجى التواصل مع مدير النظام.",
                    "صلاحية مقفلة", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }
        catch { OnLogout?.Invoke(this, EventArgs.Empty); }
    }

    private async Task CheckLockPermissionAsync()
    {
        if (_currentUserId == Guid.Empty) { OnLock?.Invoke(this, EventArgs.Empty); return; }
        OnLock?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Stores the authenticated user context and shows/hides nav items based on permissions.
    /// </summary>
    public void SetUserContext(Guid userId, string userName, string role, List<string> permissions)
    {
        _currentUserId = userId;
        _currentRole = role;
        _lblCurrentUser.Text = $"المستخدم: {userName}";

        // Apply permission visibility
        foreach (var (permission, panel) in _navPanelsByPermission)
        {
            var hasPermission = permissions.Count == 0 ||
                permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) ||
                role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
            panel.Visible = hasPermission;
        }

        // Highlight dashboard by default
        if (_navPanelsByPermission.TryGetValue("ViewDashboard", out var dashItem))
            SetActiveNavItem(dashItem);
    }

    public void SetShiftInfo(string shiftInfo)
    {
        _lblShift.Text = shiftInfo;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clockTimer?.Stop();
            _clockTimer?.Dispose();
            _notificationTimer?.Stop();
            _notificationTimer?.Dispose();
            _notificationService.NotificationRaised -= OnNotificationRaised;
            _notificationPopup?.Dispose();
            _defaultLookAndFeel?.Dispose();
        }
        base.Dispose(disposing);
    }
}
