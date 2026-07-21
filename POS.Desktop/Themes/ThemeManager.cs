namespace POS.Desktop.Themes;
using System.Drawing;
using System.Windows.Forms;

public static class ThemeManager
{
    public static void ApplyRTL(Control control)
    {
        if (control == null) return;
        control.RightToLeft = RightToLeft.Yes;
        if (control is Form form)
            form.RightToLeftLayout = true;
        foreach (Control child in control.Controls)
            ApplyRTL(child);
    }

    public static void ApplyFont(Control control, Font? font)
    {
        if (control == null) return;
        if (font != null) control.Font = font;
        foreach (Control child in control.Controls)
            ApplyFont(child, font);
    }

    public static Panel CreateLoadingPanel(string message = "جاري التحميل...")
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var lbl = new Label
        {
            Text = message,
            Font = Typography.Body,
            ForeColor = Colors.TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        var spinner = new ProgressBar { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Dock = DockStyle.Top, Height = 30, Margin = new Padding(0, 100, 0, 0) };
        panel.Controls.Add(spinner);
        panel.Controls.Add(lbl);
        return panel;
    }

    public static Panel CreateEmptyPanel(string message, string actionText, EventHandler? actionClick = null)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var lbl = new Label { Text = message, Font = Typography.Body, ForeColor = Colors.TextSecondary, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
        if (actionText != null && actionClick != null)
        {
            var btn = new Button { Text = actionText, FlatStyle = FlatStyle.Flat, Height = ControlHeight.Standard, Width = 200, Anchor = AnchorStyles.Bottom };
            btn.Click += actionClick;
            btn.Location = new Point(panel.Width / 2 - 100, panel.Height / 2 + 30);
            panel.Controls.Add(btn);
        }
        panel.Controls.Add(lbl);
        return panel;
    }

    public static Panel CreateErrorPanel(string message, EventHandler? retryClick = null)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var lbl = new Label { Text = message, Font = Typography.Body, ForeColor = Colors.Error, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
        if (retryClick != null)
        {
            var btn = new Button { Text = "إعادة المحاولة", FlatStyle = FlatStyle.Flat, Height = ControlHeight.Standard, Width = 150 };
            btn.Click += retryClick;
            panel.Controls.Add(btn);
        }
        panel.Controls.Add(lbl);
        return panel;
    }
}