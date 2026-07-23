namespace POS.Desktop.Themes;
using System.Drawing;
using System.Drawing.Drawing2D;

/// <summary>
/// Centralized Design Token System — POS_EN.md §7.
/// All visual values MUST be derived from this class. No screen may invent its own.
/// Modern Arabic POS design: warm neutrals, deep teal primary, generous whitespace,
/// soft shadows, and rounded surfaces for a professional, touch-friendly experience.
/// </summary>
public static class DesignTokens
{
    // ========================================================================
    // Spacing (px) — POS_EN.md §7.1
    // ========================================================================
    public static class Spacing
    {
        public const int Micro = 4;
        public const int Small = 8;
        public const int Compact = 12;
        public const int Standard = 16;
        public const int Medium = 20;
        public const int Section = 24;
        public const int Major = 32;
        public const int Large = 40;
        public const int Page = 48;
        public const int Xxl = 64;
    }

    // ========================================================================
    // Control Heights (px) — POS_EN.md §7.2
    // ========================================================================
    public static class ControlHeight
    {
        public const int Compact = 32;
        public const int Standard = 40;
        public const int Large = 46;
        public const int Touch = 52;
        public const int Extra = 56;
    }

    // ========================================================================
    // Border Radius (px)
    // ========================================================================
    public static class Radius
    {
        public const int None = 0;
        public const int Sm = 4;
        public const int Md = 8;
        public const int Lg = 12;
        public const int Xl = 16;
        public const int Xxl = 24;
        public const int Full = 999;
    }

    // ========================================================================
    // Elevation / Shadows (semi-transparent blacks)
    // ========================================================================
    public static class Elevation
    {
        public static readonly Color ShadowXs = Color.FromArgb(6, 0, 0, 0);
        public static readonly Color ShadowSm = Color.FromArgb(10, 0, 0, 0);
        public static readonly Color ShadowMd = Color.FromArgb(16, 0, 0, 0);
        public static readonly Color ShadowLg = Color.FromArgb(24, 0, 0, 0);
        public static readonly Color ShadowXl = Color.FromArgb(32, 0, 0, 0);

        public static void DrawShadow(Graphics g, Rectangle bounds, Color shadowColor, int offsetY, int blur)
        {
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddRectangle(new Rectangle(bounds.X, bounds.Y + offsetY, bounds.Width, bounds.Height));
            using var brush = new System.Drawing.Drawing2D.PathGradientBrush(path)
            {
                CenterColor = shadowColor,
                SurroundColors = new[] { Color.Transparent }
            };
            g.FillPath(brush, path);
        }
    }

    // ========================================================================
    // Colors — POS_EN.md §7.4
    // Modern professional Arabic palette: warm neutrals + deep teal primary.
    // ========================================================================
    public static class Colors
    {
        // Brand — deep teal / indigo blend
        public static readonly Color Primary = Color.FromArgb(13, 148, 136);       // teal-600
        public static readonly Color PrimaryHover = Color.FromArgb(15, 118, 110);  // teal-700
        public static readonly Color PrimaryPressed = Color.FromArgb(17, 94, 89);  // teal-800
        public static readonly Color PrimaryLight = Color.FromArgb(204, 251, 241); // teal-100
        public static readonly Color PrimaryLighter = Color.FromArgb(240, 253, 250); // teal-50
        public static readonly Color PrimarySoft = Color.FromArgb(20, 184, 166);   // teal-500

        // Secondary accent — warm coral
        public static readonly Color Accent = Color.FromArgb(244, 63, 94);         // rose-500
        public static readonly Color AccentHover = Color.FromArgb(225, 29, 72);    // rose-600
        public static readonly Color AccentLight = Color.FromArgb(255, 228, 230);   // rose-100

        // Surfaces
        public static readonly Color Surface = Color.FromArgb(255, 255, 255);
        public static readonly Color Background = Color.FromArgb(248, 250, 252);   // slate-50
        public static readonly Color BackgroundWarm = Color.FromArgb(250, 250, 249); // stone-50
        public static readonly Color Card = Color.FromArgb(255, 255, 255);
        public static readonly Color CardHover = Color.FromArgb(249, 250, 251);     // zinc-50
        public static readonly Color CardActive = Color.FromArgb(240, 253, 250);  // teal-50

        // Borders
        public static readonly Color Border = Color.FromArgb(226, 232, 240);      // slate-200
        public static readonly Color BorderLight = Color.FromArgb(241, 245, 249); // slate-100
        public static readonly Color BorderFocus = Color.FromArgb(20, 184, 166);  // teal-500
        public static readonly Color Divider = Color.FromArgb(226, 232, 240);

        // Text
        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);      // slate-900
        public static readonly Color TextSecondary = Color.FromArgb(100, 116, 139); // slate-500
        public static readonly Color TextHint = Color.FromArgb(148, 163, 184);      // slate-400
        public static readonly Color TextMuted = Color.FromArgb(71, 85, 105);      // slate-600
        public static readonly Color TextOnPrimary = Color.FromArgb(255, 255, 255);
        public static readonly Color TextOnDark = Color.FromArgb(241, 245, 249);   // slate-100

        // Semantic
        public static readonly Color Success = Color.FromArgb(16, 185, 129);       // emerald-500
        public static readonly Color SuccessLight = Color.FromArgb(209, 250, 229); // emerald-100
        public static readonly Color SuccessSoft = Color.FromArgb(236, 253, 245);  // emerald-50
        public static readonly Color Warning = Color.FromArgb(245, 158, 11);       // amber-500
        public static readonly Color WarningLight = Color.FromArgb(254, 243, 199); // amber-100
        public static readonly Color WarningSoft = Color.FromArgb(255, 251, 235);   // amber-50
        public static readonly Color Error = Color.FromArgb(239, 68, 68);          // red-500
        public static readonly Color ErrorLight = Color.FromArgb(254, 226, 226);   // red-100
        public static readonly Color ErrorSoft = Color.FromArgb(254, 242, 242);    // red-50
        public static readonly Color Info = Color.FromArgb(14, 165, 233);          // sky-500
        public static readonly Color InfoLight = Color.FromArgb(186, 230, 253);   // sky-200
        public static readonly Color InfoSoft = Color.FromArgb(240, 249, 255);     // sky-50

        // States
        public static readonly Color Disabled = Color.FromArgb(203, 213, 225);    // slate-300
        public static readonly Color DisabledBg = Color.FromArgb(241, 245, 249);   // slate-100
        public static readonly Color DisabledText = Color.FromArgb(148, 163, 184); // slate-400
        public static readonly Color HoverOverlay = Color.FromArgb(12, 0, 0, 0);
        public static readonly Color PressedOverlay = Color.FromArgb(20, 0, 0, 0);

        // Table
        public static readonly Color TableHeader = Color.FromArgb(248, 250, 252); // slate-50
        public static readonly Color TableRowAlt = Color.FromArgb(248, 250, 252);
        public static readonly Color TableRowHover = Color.FromArgb(240, 253, 250); // teal-50
        public static readonly Color TableRowSelected = Color.FromArgb(204, 251, 241); // teal-100

        // Shadows (semi-transparent blacks)
        public static readonly Color ShadowSm = Color.FromArgb(10, 0, 0, 0);
        public static readonly Color ShadowMd = Color.FromArgb(16, 0, 0, 0);
        public static readonly Color ShadowLg = Color.FromArgb(24, 0, 0, 0);

        // Special
        public static readonly Color Danger = Color.FromArgb(239, 68, 68);
        public static readonly Color DangerHover = Color.FromArgb(220, 38, 38);
        public static readonly Color CashDrawer = Color.FromArgb(120, 113, 108);  // stone-500

        // Dark mode ready (sidebar / shell)
        public static readonly Color SidebarBackground = Color.FromArgb(15, 23, 42);     // slate-900
        public static readonly Color SidebarBackgroundDarker = Color.FromArgb(10, 16, 30); // custom dark
        public static readonly Color SidebarHover = Color.FromArgb(30, 41, 59);          // slate-800
        public static readonly Color SidebarActive = Color.FromArgb(13, 148, 136);       // teal-600
        public static readonly Color SidebarText = Color.FromArgb(148, 163, 184);        // slate-400
        public static readonly Color SidebarTextActive = Color.FromArgb(255, 255, 255);
        public static readonly Color SidebarDivider = Color.FromArgb(51, 65, 85);        // slate-700

        // Glass effect overlays
        public static readonly Color GlassLight = Color.FromArgb(180, 255, 255, 255);
        public static readonly Color GlassDark = Color.FromArgb(140, 15, 23, 42);
    }

    // ========================================================================
    // Typography — POS_EN.md §7.3
    // Arabic: Cairo (loaded via FontLoader), English: Segoe UI
    // All fonts created statically so they can be referenced anywhere
    // ========================================================================
    public static class Typography
    {
        // Arabic fonts (Cairo or fallback)
        public static readonly Font AppTitle = Icons.FontLoader.GetArabicFont(22f, FontStyle.Bold);
        public static readonly Font PageTitle = Icons.FontLoader.GetArabicFont(20f, FontStyle.Bold);
        public static readonly Font SectionTitle = Icons.FontLoader.GetArabicFont(15f, FontStyle.Bold);
        public static readonly Font CardTitle = Icons.FontLoader.GetArabicFont(13f, FontStyle.Bold);
        public static readonly Font Body = Icons.FontLoader.GetArabicFont(10.5f);
        public static readonly Font BodyBold = Icons.FontLoader.GetArabicFont(10.5f, FontStyle.Bold);
        public static readonly Font Secondary = Icons.FontLoader.GetArabicFont(9.5f);
        public static readonly Font Caption = Icons.FontLoader.GetArabicFont(8.5f);
        public static readonly Font Button = Icons.FontLoader.GetArabicFont(10.5f);
        public static readonly Font ButtonBold = Icons.FontLoader.GetArabicFont(10.5f, FontStyle.Bold);
        public static readonly Font Table = Icons.FontLoader.GetArabicFont(10f);
        public static readonly Font TableHeader = Icons.FontLoader.GetArabicFont(10f, FontStyle.Bold);
        public static readonly Font Input = Icons.FontLoader.GetArabicFont(11f);
        public static readonly Font LargeNumber = Icons.FontLoader.GetArabicFont(28f, FontStyle.Bold);
        public static readonly Font Display = Icons.FontLoader.GetArabicFont(32f, FontStyle.Bold);
        public static readonly Font Hero = Icons.FontLoader.GetArabicFont(48f, FontStyle.Bold);

        // Icon fonts (Font Awesome)
        public static readonly Font IconSm = Icons.FontLoader.GetFontAwesomeSolid(12f);
        public static readonly Font IconMd = Icons.FontLoader.GetFontAwesomeSolid(16f);
        public static readonly Font IconLg = Icons.FontLoader.GetFontAwesomeSolid(22f);
        public static readonly Font IconXl = Icons.FontLoader.GetFontAwesomeSolid(32f);
        public static readonly Font IconXs = Icons.FontLoader.GetFontAwesomeSolid(10f);
    }

    // ========================================================================
    // Backward-Compatible Flat Aliases
    // These allow existing forms to gradually migrate to the new token system
    // ========================================================================
    public static readonly Color PrimaryColor = Colors.Primary;
    public static readonly Color PrimaryDarkColor = Colors.PrimaryHover;
    public static readonly Color SecondaryColor = Colors.Success;
    public static readonly Color AccentColor = Colors.Accent;
    public static readonly Color BackgroundColor = Colors.Background;
    public static readonly Color SurfaceColor = Colors.Surface;
    public static readonly Color CardColor = Colors.Card;
    public static readonly Color TextPrimaryColor = Colors.TextPrimary;
    public static readonly Color TextSecondaryColor = Colors.TextSecondary;
    public static readonly Color TextHintColor = Colors.TextHint;
    public static readonly Color BorderColor = Colors.Border;
    public static readonly Color ErrorColor = Colors.Error;
    public static readonly Color WarningColor = Colors.Warning;
    public static readonly Color SuccessColor = Colors.Success;
    public static readonly Color InfoColor = Colors.Info;
    public static readonly Color DisabledColor = Colors.Disabled;
    public static readonly Color DangerColor = Colors.Danger;
    public static readonly Color AvailableColor = Colors.Success;
    public static readonly Color OccupiedColor = Colors.Error;
    public static readonly Color PreparingColor = Color.FromArgb(245, 158, 11);
    public static readonly Color ReadyColor = Color.FromArgb(16, 185, 129);
    public static readonly Color WaitingForPaymentColor = Color.FromArgb(168, 85, 247);
    public static readonly Color ReservedColor = Colors.Warning;
    public static readonly Color CleaningColor = Colors.Info;
    public static readonly Color CashDrawerColor = Colors.CashDrawer;
    public static readonly Font DefaultFont = Typography.Body;
    public static readonly Font ButtonFont = Typography.Button;
    public static readonly Font HeadingFont = Typography.PageTitle;
    public static readonly Font SubheadingFont = Typography.SectionTitle;
    public static readonly Font SmallFont = Typography.Caption;
    public static readonly Font DataFont = Typography.Table;
    public static readonly int SpacingXS = Spacing.Micro;
    public static readonly int SpacingSM = Spacing.Small;
    public static readonly int SpacingMD = Spacing.Standard;
    public static readonly int SpacingLG = Spacing.Section;
    public static readonly int SpacingXL = Spacing.Major;
    public static readonly int SpacingXXL = Spacing.Page;
    public static readonly int BorderRadius = Radius.Md;
    public static readonly int BorderWidth = 1;

    // ========================================================================
    // Currency Formatting — POS_EN.md §5
    // ========================================================================
    public static string FormatJOD(decimal amount) => amount.ToString("N3") + " JOD";

    // ========================================================================
    // Helper methods for modern UI
    // ========================================================================
    public static GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int maxRadius = Math.Min(rect.Width, rect.Height) / 2;
        int r = Math.Min(radius, maxRadius);
        int d = r * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static Color WithAlpha(this Color color, int alpha)
    {
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }
}
