namespace POS.Desktop.Themes;
using System.Drawing;
using System.Drawing.Drawing2D;
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
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes
        };
        var spinner = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Dock = DockStyle.Top,
            Height = 4,
            Margin = new Padding(0, 100, 0, 0)
        };
        panel.Controls.Add(spinner);
        panel.Controls.Add(lbl);
        return panel;
    }

    public static Panel CreateEmptyPanel(string message, string actionText, EventHandler? actionClick = null)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Colors.Surface,
            Padding = new Padding(Spacing.Page)
        };

        var container = new Panel
        {
            Width = 320,
            Height = 160,
            BackColor = Colors.Surface
        };
        container.Location = new Point((panel.Width - container.Width) / 2, (panel.Height - container.Height) / 2);

        var iconLabel = new Label
        {
            Text = FontAwesomeIcons.Info,
            Font = Icons.FontLoader.GetFontAwesomeSolid(48f),
            ForeColor = Colors.TextHint,
            Size = new Size(64, 64),
            Location = new Point((container.Width - 64) / 2, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var lbl = new Label
        {
            Text = message,
            Font = Typography.Body,
            ForeColor = Colors.TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(container.Width, 44),
            Location = new Point(0, 72),
            RightToLeft = RightToLeft.Yes
        };

        container.Controls.Add(iconLabel);
        container.Controls.Add(lbl);

        if (actionText != null && actionClick != null)
        {
            var btn = new CustomControls.RtlButton
            {
                Text = actionText,
                Type = CustomControls.RtlButton.ButtonType.Primary,
                Size = new Size(200, ControlHeight.Standard),
                Location = new Point((container.Width - 200) / 2, 120),
                CornerRadius = Radius.Md
            };
            btn.Click += actionClick;
            container.Controls.Add(btn);
        }

        panel.Controls.Add(container);
        panel.Resize += (s, e) =>
        {
            container.Location = new Point((panel.Width - container.Width) / 2, (panel.Height - container.Height) / 2);
        };
        return panel;
    }

    public static Panel CreateErrorPanel(string message, EventHandler? retryClick = null)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Colors.Surface,
            Padding = new Padding(Spacing.Page)
        };

        var container = new Panel
        {
            Width = 320,
            Height = 180,
            BackColor = Colors.Surface
        };
        container.Location = new Point((panel.Width - container.Width) / 2, (panel.Height - container.Height) / 2);

        var iconLabel = new Label
        {
            Text = FontAwesomeIcons.Error,
            Font = Icons.FontLoader.GetFontAwesomeSolid(48f),
            ForeColor = Colors.Error,
            Size = new Size(64, 64),
            Location = new Point((container.Width - 64) / 2, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var lbl = new Label
        {
            Text = message,
            Font = Typography.Body,
            ForeColor = Colors.Error,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(container.Width, 44),
            Location = new Point(0, 72),
            RightToLeft = RightToLeft.Yes
        };

        container.Controls.Add(iconLabel);
        container.Controls.Add(lbl);

        if (retryClick != null)
        {
            var btn = new CustomControls.RtlButton
            {
                Text = "إعادة المحاولة",
                Type = CustomControls.RtlButton.ButtonType.Secondary,
                Size = new Size(160, ControlHeight.Standard),
                Location = new Point((container.Width - 160) / 2, 124),
                CornerRadius = Radius.Md
            };
            btn.Click += retryClick;
            container.Controls.Add(btn);
        }

        panel.Controls.Add(container);
        panel.Resize += (s, e) =>
        {
            container.Location = new Point((panel.Width - container.Width) / 2, (panel.Height - container.Height) / 2);
        };
        return panel;
    }

    public static Panel CreateStatusBadge(string text, Color backColor, Color foreColor, int height = 28)
    {
        var panel = new Panel
        {
            BackColor = backColor,
            Height = height,
            Padding = new Padding(Spacing.Small, 0, Spacing.Small, 0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        var lbl = new Label
        {
            Text = text,
            Font = Typography.Caption,
            ForeColor = foreColor,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = true,
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes
        };
        panel.Controls.Add(lbl);
        panel.Paint += (s, e) =>
        {
            var p = (Panel)s!;
            using var path = DesignTokens.CreateRoundedRect(p.ClientRectangle, Radius.Md);
            p.Region = new Region(path);
        };
        return panel;
    }

    public static void DrawCardShadow(Graphics g, Rectangle bounds)
    {
        const int shadowSize = 8;
        for (int i = 0; i < shadowSize; i++)
        {
            var alpha = 8 - i;
            using var pen = new Pen(Color.FromArgb(alpha, 0, 0, 0), 1);
            var rect = new Rectangle(bounds.X + i, bounds.Y + i, bounds.Width - i * 2, bounds.Height - i * 2);
            g.DrawRectangle(pen, rect);
        }
    }
}
