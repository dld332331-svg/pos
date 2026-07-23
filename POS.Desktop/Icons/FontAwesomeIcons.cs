namespace POS.Desktop.Icons;

/// <summary>
/// Font Awesome 6 Free icon constants (Unicode characters).
/// Organized by categories matching POS_EN.md spec Section 11.1.
/// Use these constants to display icons: label.Text = FontAwesomeIcons.User;
/// Set label.Font = FontLoader.GetFontAwesomeSolid(14f);
/// </summary>
public static class FontAwesomeIcons
{
    // ---- CRUD Actions ----
    public const string Add = "\u002b";          // fa-plus
    public const string Edit = "\u270f";         // fa-pen-to-square (or ✏)
    public const string Delete = "\uf2ed";       // fa-trash-can
    public const string Save = "\uf0c7";         // fa-floppy-disk
    public const string Cancel = "\uf00d";       // fa-xmark

    // ---- Navigation & Search ----
    public const string Search = "\uf002";       // fa-magnifying-glass
    public const string Filter = "\uf0b0";       // fa-filter
    public const string Refresh = "\uf2f1";      // fa-rotate
    public const string Menu = "\uf0c9";         // fa-bars (fa-navicon)
    public const string Home = "\uf015";         // fa-house
    public const string Back = "\uf060";         // fa-arrow-left
    public const string Forward = "\uf061";      // fa-arrow-right
    public const string Next = "\uf178";         // fa-arrow-right-long
    public const string Previous = "\uf177";     // fa-arrow-left-long
    public const string ChevronDown = "\uf078";  // fa-chevron-down
    public const string ChevronUp = "\uf077";    // fa-chevron-up
    public const string ChevronLeft = "\uf053";  // fa-chevron-left
    public const string ChevronRight = "\uf054"; // fa-chevron-right
    public const string Expand = "\uf065";       // fa-expand
    public const string Collapse = "\uf066";     // fa-compress

    // ---- POS & Sales ----
    public const string PosTerminal = "\uf07a";  // fa-cart-shopping
    public const string Sale = "\uf291";         // fa-credit-card
    public const string Payment = "\uf09d";       // fa-credit-card
    public const string Cash = "\uf155";          // fa-dollar-sign
    public const string Card = "\uf09d";          // fa-credit-card
    public const string Receipt = "\uf543";       // fa-receipt
    public const string Discount = "\uf53b";      // fa-tags
    public const string Tax = "\uf0ac";           // fa-globe (general, or fa-coins)
    public const string Invoice = "\uf15c";       // fa-file-invoice
    public const string Return = "\uf0e2";        // fa-rotate-left (undo)
    public const string Hold = "\uf04e";          // fa-pause (fa-pause-circle)
    public const string Retrieve = "\uf04a";      // fa-play (fa-play-circle)

    // ---- Products & Categories ----
    public const string Product = "\uf1b3";       // fa-cube
    public const string Products = "\uf1b2";      // fa-cubes
    public const string Category = "\uf2db";      // fa-folder-tree
    public const string Barcode = "\uf02a";       // fa-barcode
    public const string Weight = "\uf496";        // fa-weight-scale
    public const string Package = "\uf4ed";       // fa-box

    // ---- Inventory ----
    public const string Inventory = "\uf48b";     // fa-boxes-stacked (or fa-warehouse)
    public const string Warehouse = "\uf494";     // fa-warehouse
    public const string Stock = "\uf466";         // fa-chart-line (or fa-cubes)
    public const string LowStock = "\uf071";      // fa-triangle-exclamation (warning)
    public const string Movement = "\uf0ec";      // fa-right-left (arrows)
    public const string Adjust = "\uf04f";        // fa-sliders (or fa-pen-to-square)

    // ---- Reports ----
    public const string Report = "\uf080";        // fa-chart-bar
    public const string Chart = "\uf0e4";          // fa-chart-simple (or fa-chart-line)
    public const string PieChart = "\uf200";       // fa-chart-pie
    public const string Profit = "\uf0d6";         // fa-money-bill-trend-up (generic fa-chart-line)

    // ---- Restaurant ----
    public const string Table = "\uf0ce";          // fa-table
    public const string TableOccupied = "\uf007";  // fa-user
    public const string Kitchen = "\uf2e1";        // fa-utensils (or fa-kitchen-set if exists)
    public const string Utensils = "\uf2e7";       // fa-utensils (correction)
    public const string MenuItem = "\uf818";       // fa-turkey (or fa-bowl-food)
    public const string Order = "\uf291";          // fa-clipboard-list

    // ---- Users & Security ----
    public const string User = "\uf007";           // fa-user
    public const string Users = "\uf0c0";          // fa-users
    public const string UserAdd = "\uf234";        // fa-user-plus
    public const string UserLock = "\uf502";       // fa-user-lock
    public const string Lock = "\uf023";           // fa-lock
    public const string Unlock = "\uf09c";         // fa-unlock
    public const string Eye = "\uf06e";            // fa-eye
    public const string EyeSlash = "\uf070";       // fa-eye-slash
    public const string Logout = "\uf08b";         // fa-right-from-bracket (or fa-sign-out-alt)
    public const string Login = "\uf090";          // fa-right-to-bracket (or fa-sign-in-alt)
    public const string Shield = "\uf3ed";         // fa-shield-halved
    public const string Key = "\uf084";            // fa-key
    public const string Permission = "\uf2db";     // fa-lock (or fa-user-gear)

    // ---- System & Settings ----
    public const string Settings = "\uf013";       // fa-gear (fa-cog)
    public const string Database = "\uf1c0";       // fa-database
    public const string Backup = "\uf1da";         // fa-database (or fa-floppy-disk)
    public const string Restore = "\uf0e2";        // fa-rotate-left
    public const string Printer = "\uf02f";        // fa-print
    public const string PrinterError = "\uf071";   // fa-triangle-exclamation
    public const string Print = "\uf02f";          // fa-print
    public const string Config = "\uf013";         // fa-gear

    // ---- Notifications & Status ----
    public const string Success = "\uf058";        // fa-circle-check
    public const string Error = "\uf06a";          // fa-circle-exclamation
    public const string Warning = "\uf071";        // fa-triangle-exclamation
    public const string Info = "\uf05a";           // fa-circle-info
    public const string Question = "\uf059";       // fa-circle-question
    public const string Notification = "\uf0f3";   // fa-bell
    public const string Alert = "\uf0f3";          // fa-bell
    public const string Loading = "\uf110";        // fa-spinner (animated)

    // ---- Time & Date ----
    public const string Calendar = "\uf133";       // fa-calendar
    public const string Clock = "\uf017";          // fa-clock
    public const string History = "\uf1da";        // fa-clock-rotate-left (fa-history)

    // ---- Communication ----
    public const string Email = "\uf0e0";          // fa-envelope
    public const string Phone = "\uf095";          // fa-phone
    public const string Address = "\uf3c5";        // fa-location-dot (fa-map-marker-alt)
    public const string Customer = "\uf007";       // fa-user
    public const string Supplier = "\uf0f7";       // fa-truck-moving (or fa-truck)
    public const string Notes = "\uf249";          // fa-note-sticky (fa-sticky-note)

    // ---- Actions ----
    public const string Check = "\uf00c";          // fa-check
    public const string Close = "\uf00d";          // fa-xmark
    public const string Plus = "\uf067";           // fa-plus
    public const string Minus = "\uf068";          // fa-minus
    public const string Download = "\uf019";       // fa-download
    public const string Upload = "\uf093";         // fa-upload
    public const string Export = "\uf56d";         // fa-file-export
    public const string Import = "\uf56f";         // fa-file-import
    public const string Attach = "\uf0c6";         // fa-paperclip

    // ---- Misc ----
    public const string Dashboard = "\uf0e4";      // fa-gauge-high (or fa-chart-simple)
    public const string DashboardAlt = "\uf3fd";   // fa-gauge
    public const string Money = "\uf3d1";           // fa-money-bill
    public const string MoneyBills = "\uf0d6";     // fa-money-bills
    public const string Shift = "\uf254";          // fa-arrows-rotate (or fa-clock)
    public const string Register = "\uf0f6";       // fa-registered (generic)
    public const string Expense = "\uf53a";         // fa-money-bill-wave (or fa-cash-register)
    public const string Withdrawal = "\uf063";     // fa-arrow-down (or fa-money-bill-transfer)
    public const string Deposit = "\uf062";        // fa-arrow-up (or fa-money-bill-transfer)
    public const string Transfer = "\uf362";       // fa-arrow-right-arrow-left
    public const string Version = "\uf021";        // fa-rotate (or fa-code-fork)
    public const string QrCode = "\uf029";         // fa-qrcode
    public const string Copyright = "\uf1f9";        // fa-copyright

    /// <summary>
    /// Returns the icon character for the given category, or empty string if not found.
    /// Useful for dynamic icon lookup.
    /// </summary>
    public static string GetIcon(string iconName)
    {
        var field = typeof(FontAwesomeIcons).GetField(iconName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        return field?.GetValue(null)?.ToString() ?? "";
    }
}
