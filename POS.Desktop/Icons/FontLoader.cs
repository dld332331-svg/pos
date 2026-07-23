using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace POS.Desktop.Icons;

/// <summary>
/// Loads and provides access to embedded font files (Font Awesome, Cairo Arabic) at runtime.
/// Fonts are embedded as resources in POS.Desktop and loaded via PrivateFontCollection.
/// </summary>
public static class FontLoader
{
    private static readonly PrivateFontCollection _fontCollection = new PrivateFontCollection();
    private static bool _initialized;

    // Font family names (must match the actual font names in the files)
    public const string FontAwesomeSolidFamily = "Font Awesome 6 Free Solid";
    public const string FontAwesomeRegularFamily = "Font Awesome 6 Free Regular";
    public const string CairoFamily = "Cairo";

    /// <summary>
    /// Initializes font collection by loading embedded font resources.
    /// Must be called once at application startup (before any form is created).
    /// Safe to call multiple times.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        var assembly = Assembly.GetExecutingAssembly();
        var resourcePrefix = "POS.Desktop.Resources.Fonts";

        // Load Font Awesome Solid
        LoadFontFromResource(assembly, $"{resourcePrefix}.FontAwesome6FreeSolid.otf");
        // Load Font Awesome Regular
        LoadFontFromResource(assembly, $"{resourcePrefix}.FontAwesome6FreeRegular.otf");
        // Load Cairo Arabic font
        LoadFontFromResource(assembly, $"{resourcePrefix}.Cairo-Variable.ttf");
    }

    /// <summary>
    /// Gets a Font Awesome Solid font at the specified size.
    /// </summary>
    public static Font GetFontAwesomeSolid(float size, FontStyle style = FontStyle.Regular)
    {
        Initialize();
        return new Font(FontAwesomeSolidFamily, size, style, GraphicsUnit.Point);
    }

    /// <summary>
    /// Gets a Font Awesome Regular font at the specified size.
    /// </summary>
    public static Font GetFontAwesomeRegular(float size, FontStyle style = FontStyle.Regular)
    {
        Initialize();
        return new Font(FontAwesomeRegularFamily, size, style, GraphicsUnit.Point);
    }

    /// <summary>
    /// Gets the Cairo Arabic font at the specified size.
    /// </summary>
    public static Font GetCairoFont(float size, FontStyle style = FontStyle.Regular)
    {
        Initialize();
        return new Font(CairoFamily, size, style, GraphicsUnit.Point);
    }

    /// <summary>
    /// Creates a Font from a size and style, falling back from Cairo -> Segoe UI -> Microsoft Sans Serif.
    /// </summary>
    public static Font GetArabicFont(float size, FontStyle style = FontStyle.Regular)
    {
        try
        {
            Initialize();
            // Try loading Cairo first if available
            if (IsFontInstalled(CairoFamily))
                return new Font(CairoFamily, size, style, GraphicsUnit.Point);
        }
        catch { System.Diagnostics.Trace.TraceWarning("[FontLoader] Cairo font not available, falling back to Segoe UI"); }

        // Fallback to Segoe UI (excellent Arabic on Windows 10+)
        try { return new Font("Segoe UI", size, style, GraphicsUnit.Point); }
        catch { System.Diagnostics.Trace.TraceWarning("[FontLoader] Segoe UI font not available, falling back to Microsoft Sans Serif"); }

        // Final fallback
        return new Font("Microsoft Sans Serif", size, style, GraphicsUnit.Point);
    }

    /// <summary>
    /// Creates a Font Awesome icon label with the specified icon and size.
    /// </summary>
    public static Label CreateIconLabel(string iconChar, float size = 14f, Color? color = null)
    {
        return new Label
        {
            Text = iconChar,
            Font = GetFontAwesomeSolid(size),
            ForeColor = color ?? DesignTokens.Colors.TextPrimary,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(0)
        };
    }

    private static void LoadFontFromResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            System.Diagnostics.Trace.TraceWarning($"[FontLoader] Resource not found: {resourceName}");
            return;
        }

        // Read font data into a byte array
        var fontData = new byte[stream.Length];
        stream.Read(fontData, 0, fontData.Length);

        // Allocate unmanaged memory and copy font data
        var handle = Marshal.AllocCoTaskMem(fontData.Length);
        try
        {
            Marshal.Copy(fontData, 0, handle, fontData.Length);
            _fontCollection.AddMemoryFont(handle, fontData.Length);
        }
        finally
        {
            Marshal.FreeCoTaskMem(handle);
        }
    }

    private static bool IsFontInstalled(string familyName)
    {
        foreach (var font in _fontCollection.Families)
        {
            if (font.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
