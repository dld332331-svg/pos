namespace POS.Desktop.Themes;
using System.Drawing;

public static class DesignTokens
{
    // Spacing (px) - matches spec Section 7.1
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
    }

    // Control Heights - matches spec Section 7.2
    public static class ControlHeight
    {
        public const int Compact = 32;
        public const int Standard = 36;
        public const int Large = 44;
        public const int Touch = 48;
    }

    // Colors - matches spec Section 7.4
    public static class Colors
    {
        public static readonly Color Primary = Color.FromArgb(41, 98, 255);
        public static readonly Color PrimaryHover = Color.FromArgb(30, 80, 220);
        public static readonly Color PrimaryPressed = Color.FromArgb(20, 65, 190);
        public static readonly Color Surface = Color.FromArgb(255, 255, 255);
        public static readonly Color Background = Color.FromArgb(245, 245, 248);
        public static readonly Color Border = Color.FromArgb(220, 220, 225);
        public static readonly Color TextPrimary = Color.FromArgb(33, 33, 33);
        public static readonly Color TextSecondary = Color.FromArgb(117, 117, 117);
        public static readonly Color Success = Color.FromArgb(46, 160, 67);
        public static readonly Color Warning = Color.FromArgb(255, 152, 0);
        public static readonly Color Error = Color.FromArgb(229, 57, 53);
        public static readonly Color Info = Color.FromArgb(25, 118, 210);
        public static readonly Color Disabled = Color.FromArgb(189, 189, 189);
        public static readonly Color TableHeader = Color.FromArgb(248, 248, 252);
        public static readonly Color TableRowAlt = Color.FromArgb(250, 250, 255);
        public static readonly Color Danger = Color.FromArgb(229, 57, 53);
        public static readonly Color DangerHover = Color.FromArgb(198, 40, 40);
        public static readonly Color SuccessLight = Color.FromArgb(232, 245, 233);
        public static readonly Color WarningLight = Color.FromArgb(255, 243, 224);
        public static readonly Color ErrorLight = Color.FromArgb(253, 237, 236);
    }

    // Typography - matches spec Section 7.3
    // Arabic fonts: Uses Cairo font (loaded via FontLoader) with Segoe UI fallback
    public static class Typography
    {
        // === English/Latin Fonts (Segoe UI) ===
        public static readonly Font AppTitle = new Font("Segoe UI", 18f, FontStyle.Bold);
        public static readonly Font PageTitle = new Font("Segoe UI", 16f, FontStyle.Bold);
        public static readonly Font SectionTitle = new Font("Segoe UI", 13f, FontStyle.Bold);
        public static readonly Font CardTitle = new Font("Segoe UI", 12f, FontStyle.Bold);
        public static readonly Font Body = new Font("Segoe UI", 10f);
        public static readonly Font BodyBold = new Font("Segoe UI", 10f, FontStyle.Bold);
        public static readonly Font Secondary = new Font("Segoe UI", 9f);
        public static readonly Font Caption = new Font("Segoe UI", 8f);
        public static readonly Font Button = new Font("Segoe UI", 10f);
        public static readonly Font ButtonBold = new Font("Segoe UI", 10f, FontStyle.Bold);
        public static readonly Font Table = new Font("Segoe UI", 9.5f);
        public static readonly Font TableHeader = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font Input = new Font("Segoe UI", 10f);

        // === Arabic Fonts (Cairo via FontLoader, fallback Segoe UI) ===
        // Loaded lazily via helper in Icons.FontLoader
        public static readonly Font ArabicBody = Icons.FontLoader.GetArabicFont(10f);
        public static readonly Font ArabicHeading = Icons.FontLoader.GetArabicFont(13f, FontStyle.Bold);
        public static readonly Font ArabicBodyBold = Icons.FontLoader.GetArabicFont(10f, FontStyle.Bold);
        public static readonly Font ArabicButton = Icons.FontLoader.GetArabicFont(10f);
        public static readonly Font ArabicButtonBold = Icons.FontLoader.GetArabicFont(10f, FontStyle.Bold);
        public static readonly Font ArabicTitle = Icons.FontLoader.GetArabicFont(16f, FontStyle.Bold);

        // === Icon Fonts (Font Awesome) ===
        // For use with FontAwesomeIcons constants
        // Create with: FontLoader.GetFontAwesomeSolid(size)
    }

    // Flat aliases used by forms (from legacy LoginForm pattern)
    public static readonly Color PrimaryColor = Colors.Primary;
    public static readonly Color PrimaryDarkColor = Color.FromArgb(0, 76, 133);
    public static readonly Color SecondaryColor = Color.FromArgb(76, 175, 80);
    public static readonly Color AccentColor = Colors.Warning;
    public static readonly Color BackgroundColor = Colors.Background;
    public static readonly Color SurfaceColor = Colors.Surface;
    public static readonly Color CardColor = Color.FromArgb(248, 249, 250);
    public static readonly Color TextPrimaryColor = Colors.TextPrimary;
    public static readonly Color TextSecondaryColor = Colors.TextSecondary;
    public static readonly Color TextHintColor = Colors.Disabled;
    public static readonly Color BorderColor = Colors.Border;
    public static readonly Color ErrorColor = Colors.Error;
    public static readonly Color WarningColor = Colors.Warning;
    public static readonly Color SuccessColor = Colors.Success;
    public static readonly Color InfoColor = Colors.Info;
    public static readonly Color DisabledColor = Colors.Disabled;
    public static readonly Color DangerColor = Colors.Danger;
    public static readonly Color AvailableColor = Colors.Success;
    public static readonly Color OccupiedColor = Colors.Error;
    public static readonly Color PreparingColor = Color.FromArgb(255, 193, 7);
    public static readonly Color ReadyColor = Color.FromArgb(0, 200, 83);
    public static readonly Color WaitingForPaymentColor = Color.FromArgb(156, 39, 176);
    public static readonly Color ReservedColor = Colors.Warning;
        public static readonly Color CleaningColor = Colors.Info;
        public static readonly Color CashDrawerColor = Color.FromArgb(141, 110, 99);
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
    public static readonly int BorderRadius = 4;
    public static readonly int BorderWidth = 1;

    // Currency formatting - matches spec Section 5
    public static string FormatJOD(decimal amount) => amount.ToString("0.000");
}
