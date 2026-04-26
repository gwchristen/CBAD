using System.Drawing;

namespace CBAD.UI;

/// <summary>
/// Centralized color definitions for light and dark themes.
/// All UI surfaces that participate in dark-mode toggling should
/// read their colors from this class to ensure consistency.
/// </summary>
internal static class AppTheme
{
    // ── Light theme ──────────────────────────────────────────────────────
    public static Color LightFormBg       => SystemColors.Control;
    public static Color LightPanelBg      => Color.FromArgb(245, 247, 250);
    public static Color LightCardBg       => Color.White;
    public static Color LightInputBg      => SystemColors.Window;
    public static Color LightInputFg      => SystemColors.WindowText;
    public static Color LightLabelFg      => SystemColors.ControlText;
    public static Color LightMutedFg      => Color.Gray;
    public static Color LightHeadingFg    => Color.FromArgb(30, 46, 78);

    // ── Dark theme ───────────────────────────────────────────────────────
    public static Color DarkFormBg        => Color.FromArgb(28, 32, 42);
    public static Color DarkPanelBg       => Color.FromArgb(35, 39, 50);
    public static Color DarkCardBg        => Color.FromArgb(42, 47, 60);
    public static Color DarkInputBg       => Color.FromArgb(50, 55, 68);
    public static Color DarkInputFg       => Color.FromArgb(210, 218, 228);
    public static Color DarkLabelFg       => Color.FromArgb(215, 220, 230);
    public static Color DarkMutedFg       => Color.FromArgb(140, 155, 175);
    public static Color DarkHeadingFg     => Color.FromArgb(130, 170, 220);

    // ── Dark theme borders ───────────────────────────────────────────────
    public static Color DarkBorderColor      => Color.FromArgb(75, 85, 110);

    // ── Convenience accessors ────────────────────────────────────────────
    public static Color PanelBg(bool dark)    => dark ? DarkPanelBg    : LightPanelBg;
    public static Color CardBg(bool dark)     => dark ? DarkCardBg     : LightCardBg;
    public static Color InputBg(bool dark)    => dark ? DarkInputBg    : LightInputBg;
    public static Color InputFg(bool dark)    => dark ? DarkInputFg    : LightInputFg;
    public static Color LabelFg(bool dark)    => dark ? DarkLabelFg    : LightLabelFg;
    public static Color MutedFg(bool dark)    => dark ? DarkMutedFg    : LightMutedFg;
    public static Color HeadingFg(bool dark)  => dark ? DarkHeadingFg  : LightHeadingFg;
    public static Color BorderColor(bool dark) => dark ? DarkBorderColor : SystemColors.ControlDark;
}
