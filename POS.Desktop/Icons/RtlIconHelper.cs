namespace POS.Desktop.Icons;

/// <summary>
/// Provides RTL-aware icon selection for directional icons.
/// In RTL mode, left-pointing icons become right-pointing and vice versa.
/// Usage: label.Text = RtlIconHelper.GetIcon("Back");
/// </summary>
public static class RtlIconHelper
{
    /// <summary>
    /// Returns the appropriate FontAwesome icon character based on the current RTL state.
    /// Directional icons (arrows, chevrons, etc.) are swapped in RTL mode.
    /// </summary>
    public static string GetIcon(string iconName, bool isRtl = true)
    {
        return iconName switch
        {
            // Navigation arrows (swap left ↔ right in RTL)
            nameof(FontAwesomeIcons.Back) => isRtl ? FontAwesomeIcons.Forward : FontAwesomeIcons.Back,
            nameof(FontAwesomeIcons.Forward) => isRtl ? FontAwesomeIcons.Back : FontAwesomeIcons.Forward,
            nameof(FontAwesomeIcons.Previous) => isRtl ? FontAwesomeIcons.Next : FontAwesomeIcons.Previous,
            nameof(FontAwesomeIcons.Next) => isRtl ? FontAwesomeIcons.Previous : FontAwesomeIcons.Next,

            // Chevrons (swap left ↔ right in RTL)
            nameof(FontAwesomeIcons.ChevronLeft) => isRtl ? FontAwesomeIcons.ChevronRight : FontAwesomeIcons.ChevronLeft,
            nameof(FontAwesomeIcons.ChevronRight) => isRtl ? FontAwesomeIcons.ChevronLeft : FontAwesomeIcons.ChevronRight,

            // Directional actions
            nameof(FontAwesomeIcons.Login) => isRtl ? FontAwesomeIcons.Logout : FontAwesomeIcons.Login,
            nameof(FontAwesomeIcons.Logout) => isRtl ? FontAwesomeIcons.Login : FontAwesomeIcons.Logout,
            nameof(FontAwesomeIcons.Export) => isRtl ? FontAwesomeIcons.Import : FontAwesomeIcons.Export,
            nameof(FontAwesomeIcons.Import) => isRtl ? FontAwesomeIcons.Export : FontAwesomeIcons.Import,

            // Transfer / Movement
            nameof(FontAwesomeIcons.Transfer) => isRtl ? FontAwesomeIcons.Transfer : FontAwesomeIcons.Transfer,

            // Non-directional icons pass through
            _ => iconName
        };
    }

    /// <summary>
    /// Returns a label configured with the RTL-aware icon.
    /// </summary>
    public static Label CreateIconLabel(string iconName, float size = 14f, bool isRtl = true)
    {
        return FontLoader.CreateIconLabel(GetIcon(iconName, isRtl), size);
    }

    /// <summary>
    /// Returns the appropriate Unicode directional arrow for pagination/navigation.
    /// Uses standard Unicode arrow characters that render in any font.
    /// Previous page always shows ◀ (left-pointing), next page always shows ▶ (right-pointing).
    /// This convention is consistent across LTR and RTL UIs — the arrows indicate
    /// visual navigation direction (backward/forward through items), not reading direction.
    /// </summary>
    public static string GetPaginationArrow(bool isNext)
    {
        return isNext ? "▶" : "◀";
    }
}
