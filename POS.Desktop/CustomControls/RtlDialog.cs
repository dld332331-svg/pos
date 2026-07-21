namespace POS.Desktop.CustomControls;
using System.Drawing;
using System.Windows.Forms;
using POS.Desktop.Themes;

public class RtlDialog : Form
{
    private Panel _headerPanel = null!;
    private Label _titleLabel = null!;
    private Panel _contentPanel = null!;
    private Panel _footerPanel = null!;
    private FlowLayoutPanel _actionsPanel = null!;
    private Label? _messageLabel;
    private Panel? _loadingOverlay;
    private Panel? _successPanel;
    private Panel? _errorPanel;
    private Timer? _stateTimer;

    public string? DialogId { get; set; }
    public string? DialogPurpose { get; set; }
    public string? DialogMessage { get => _messageLabel?.Text; set { if (_messageLabel != null) _messageLabel.Text = value; } }
    public bool CloseOnOutsideClick { get; set; } = false;
    public string DialogTitle { get => _titleLabel.Text; set => _titleLabel.Text = value; }
    public Panel ContentArea => _contentPanel;

    public RtlDialog(string title, int width = 500, int height = 400)
    {
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(width, height);
        RightToLeft = RightToLeft.Yes;
        Font = Typography.Body;
        BackColor = Colors.Surface;
        MaximizeBox = false;
        MinimizeBox = false;
        ControlBox = true;

        _headerPanel = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Colors.Surface, Padding = new Padding(DesignTokens.Spacing.Standard) };
        _titleLabel = new Label { Text = title, Font = Typography.SectionTitle, ForeColor = Colors.TextPrimary, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight | ContentAlignment.MiddleCenter };
        _headerPanel.Controls.Add(_titleLabel);

        _contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = Colors.Surface, Padding = new Padding(DesignTokens.Spacing.Standard), AutoScroll = true };

        _footerPanel = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Colors.Surface, Padding = new Padding(DesignTokens.Spacing.Standard) };
        _actionsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        _footerPanel.Controls.Add(_actionsPanel);

        Controls.Add(_contentPanel);
        Controls.Add(_footerPanel);
        Controls.Add(_headerPanel);
    }

    public void ShowLoading(string message = "جاري التنفيذ...")
    {
        if (_loadingOverlay == null)
        {
            _loadingOverlay = ThemeManager.CreateLoadingPanel(message);
            _loadingOverlay.Visible = false;
            Controls.Add(_loadingOverlay);
            _loadingOverlay.BringToFront();
        }
        _loadingOverlay.Visible = true;
        if (_successPanel != null) _successPanel.Visible = false;
        if (_errorPanel != null) _errorPanel.Visible = false;
    }

    public void HideLoading()
    {
        if (_loadingOverlay != null) _loadingOverlay.Visible = false;
    }

    public void ShowSuccess(string message = "تم بنجاح", int durationMs = 2000)
    {
        HideLoading();
        if (_successPanel == null)
        {
            _successPanel = new Panel { Dock = DockStyle.Fill, BackColor = Colors.Surface, Visible = false };
            _successPanel.Controls.Add(new Label { Text = $"✓ {message}", Font = Typography.SectionTitle, ForeColor = Colors.Success, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });
            Controls.Add(_successPanel);
            _successPanel.BringToFront();
        }
        _successPanel.Visible = true;
        if (_errorPanel != null) _errorPanel.Visible = false;
        AutoClose(durationMs);
    }

    public void ShowError(string message = "حدث خطأ", int durationMs = 3000)
    {
        HideLoading();
        if (_errorPanel == null)
        {
            _errorPanel = new Panel { Dock = DockStyle.Fill, BackColor = Colors.Surface, Visible = false };
            _errorPanel.Controls.Add(new Label { Text = $"✗ {message}", Font = Typography.SectionTitle, ForeColor = Colors.Error, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });
            Controls.Add(_errorPanel);
            _errorPanel.BringToFront();
        }
        _errorPanel.Visible = true;
        if (_successPanel != null) _successPanel.Visible = false;
        AutoClose(durationMs);
    }

    private void AutoClose(int durationMs)
    {
        _stateTimer?.Stop();
        _stateTimer = new Timer { Interval = durationMs };
        _stateTimer.Tick += (s, e) =>
        {
            _stateTimer.Stop();
            if (_successPanel != null) _successPanel.Visible = false;
            if (_errorPanel != null) _errorPanel.Visible = false;
        };
        _stateTimer.Start();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (CloseOnOutsideClick)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    public void AddAction(string text, EventHandler? clickHandler, bool isPrimary = true, bool isDestructive = false)
    {
        var btn = new RtlButton
        {
            Text = text,
            Type = isDestructive ? RtlButton.ButtonType.Destructive : (isPrimary ? RtlButton.ButtonType.Primary : RtlButton.ButtonType.Secondary),
            Height = DesignTokens.ControlHeight.Standard,
            Width = 120
        };
        if (clickHandler != null) btn.Click += clickHandler;
        _actionsPanel.Controls.Add(btn);
    }

    public static DialogResult ShowConfirm(string title, string message, string confirmText = "تأكيد", string cancelText = "إلغاء")
    {
        using var dlg = new RtlDialog(title, 450, 200);
        var msgLabel = new Label { Text = message, Font = Typography.Body, ForeColor = Colors.TextPrimary, Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopRight, AutoSize = true };
        dlg._messageLabel = msgLabel;
        dlg.ContentArea.Controls.Add(msgLabel);
        var result = DialogResult.Cancel;
        dlg.AddAction(confirmText, (s, e) => { result = DialogResult.OK; dlg.Close(); }, true);
        dlg.AddAction(cancelText, (s, e) => { result = DialogResult.Cancel; dlg.Close(); }, false);
        dlg.AcceptButton = dlg._actionsPanel.Controls[0] as Button;
        dlg.CancelButton = dlg._actionsPanel.Controls[1] as Button;
        dlg.ShowDialog();
        return result;
    }

    public static DialogResult ShowDestructiveConfirm(string title, string message)
    {
        using var dlg = new RtlDialog(title, 450, 250);
        var warnIcon = new Label { Text = "⚠", Font = new Font("Segoe UI", 28f), ForeColor = Colors.Warning, Dock = DockStyle.Top, Height = 50, TextAlign = ContentAlignment.MiddleCenter };
        var msgLabel = new Label { Text = message, Font = Typography.Body, ForeColor = Colors.TextPrimary, Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopRight, AutoSize = true };
        dlg._messageLabel = msgLabel;
        dlg.ContentArea.Controls.Add(msgLabel);
        dlg.Controls.Add(warnIcon);
        var result = DialogResult.Cancel;
        dlg.AddAction("حذف", (s, e) => { result = DialogResult.OK; dlg.Close(); }, true, true);
        dlg.AddAction("إلغاء", (s, e) => { result = DialogResult.Cancel; dlg.Close(); }, false);
        dlg.AcceptButton = dlg._actionsPanel.Controls[0] as Button;
        dlg.CancelButton = dlg._actionsPanel.Controls[1] as Button;
        dlg.ShowDialog();
        return result;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _stateTimer?.Dispose();
        base.Dispose(disposing);
    }
}
