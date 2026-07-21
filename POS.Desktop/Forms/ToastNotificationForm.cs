using System.Drawing;
using System.Windows.Forms;
using POS.Domain.Interfaces;
using POS.Desktop.Themes;

namespace POS.Desktop.Forms;

/// <summary>
/// Toast notification popup that auto-dismisses after a configurable duration.
/// Supports Info, Success, Warning, and Error notification types with distinct colors.
/// </summary>
public sealed class ToastNotificationForm : Form
{
    private readonly Timer _dismissTimer;
    private readonly NotificationMessage _notification;
    private Panel _mainPanel;
    private Label _titleLabel;
    private Label _messageLabel;
    private Label _iconLabel;
    private Button _closeButton;

    private static readonly Color InfoColor = DesignTokens.InfoColor;
    private static readonly Color SuccessColor = DesignTokens.SuccessColor;
    private static readonly Color WarningColor = DesignTokens.WarningColor;
    private static readonly Color ErrorColor = DesignTokens.ErrorColor;

    private static int _toastOffset;

    public ToastNotificationForm(NotificationMessage notification)
    {
        _notification = notification;
        _dismissTimer = new Timer();

        InitializeComponent();
        PositionToast();

        if (notification.AutoDismissSeconds > 0)
        {
            _dismissTimer.Interval = notification.AutoDismissSeconds * 1000;
            _dismissTimer.Tick += (s, e) =>
            {
                _dismissTimer.Stop();
                Close();
            };
            _dismissTimer.Start();
        }
    }

    private void InitializeComponent()
    {
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        ShowInTaskbar = false;
        TopMost = true;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(360, 80);
        BackColor = DesignTokens.SurfaceColor;

        var borderColor = _notification.Type switch
        {
            NotificationType.Success => SuccessColor,
            NotificationType.Warning => WarningColor,
            NotificationType.Error => ErrorColor,
            _ => InfoColor
        };

        // Main panel with colored left border
        _mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(8, 8, 8, 8)
        };

        // Icon
        var iconChar = _notification.Type switch
        {
            NotificationType.Success => "✅",
            NotificationType.Warning => "⚠️",
            NotificationType.Error => "❌",
            _ => "ℹ️"
        };

        _iconLabel = new Label
        {
            Text = iconChar,
            Font = new Font("Segoe UI Emoji", 18f),
            Location = new Point(8, 10),
            Size = new Size(36, 36),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };

        // Title
        _titleLabel = new Label
        {
            Text = _notification.Title,
            Font = new Font(DesignTokens.DefaultFont.FontFamily, 10f, FontStyle.Bold),
            ForeColor = DesignTokens.TextPrimaryColor,
            Location = new Point(52, 8),
            Size = new Size(260, 20),
            TextAlign = ContentAlignment.MiddleRight,
            BackColor = Color.Transparent
        };

        // Message
        _messageLabel = new Label
        {
            Text = _notification.Message,
            Font = DesignTokens.SmallFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            Location = new Point(52, 30),
            Size = new Size(280, 40),
            TextAlign = ContentAlignment.MiddleRight,
            BackColor = Color.Transparent,
            AutoEllipsis = true
        };

        // Close button
        _closeButton = new Button
        {
            Text = "✕",
            Font = new Font("Segoe UI", 9f),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(20, 20),
            Location = new Point(332, 4),
            BackColor = Color.Transparent,
            ForeColor = DesignTokens.TextSecondaryColor,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _closeButton.FlatAppearance.BorderSize = 0;
        _closeButton.Click += (s, e) =>
        {
            _dismissTimer.Stop();
            Close();
        };

        // Left accent border
        var accentBorder = new Panel
        {
            Dock = DockStyle.Left,
            Width = 4,
            BackColor = borderColor
        };

        // Click to dismiss
        Click += (s, e) =>
        {
            _dismissTimer.Stop();
            _notification.OnClick?.Invoke();
            Close();
        };

        _mainPanel.Controls.Add(_closeButton);
        _mainPanel.Controls.Add(_messageLabel);
        _mainPanel.Controls.Add(_titleLabel);
        _mainPanel.Controls.Add(_iconLabel);

        Controls.Add(_mainPanel);
        Controls.Add(accentBorder);

        // Shadow effect via border
        _mainPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(40, 0, 0, 0), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };
    }

    private void PositionToast()
    {
        // Stack toasts upward from bottom-right corner
        var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
        var y = screen.Bottom - Height - 60 - _toastOffset;
        if (y < 20) _toastOffset = 0; // Reset if we've filled the screen
        var x = screen.Right - Width - 20;
        Location = new Point(x, y);
        _toastOffset += Height + 8;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _dismissTimer?.Dispose();
        _toastOffset = Math.Max(0, _toastOffset - Height - 8);
        base.OnFormClosed(e);
    }
}
