using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

using MediaColor = System.Windows.Media.Color;
using WpfApplication = System.Windows.Application;

namespace VintageStoryModManager.Services;

internal static class ThemeService
{
    private static readonly IReadOnlyDictionary<string, MediaColor> ModernThemeColors = new Dictionary<string, MediaColor>
    {
        ["IMM.AccentColor"] = MediaColor.FromArgb(0xFF, 0x4C, 0x8B, 0xF5),
        ["IMM.SurfaceColor"] = MediaColor.FromArgb(0xFF, 0xF6, 0xF8, 0xFB),
        ["IMM.SurfaceAltColor"] = MediaColor.FromArgb(0xFF, 0xEF, 0xF3, 0xF9),
        ["IMM.BorderColor"] = MediaColor.FromArgb(0xFF, 0xCA, 0xD7, 0xEB),
        ["IMM.BorderHoverColor"] = MediaColor.FromArgb(0xFF, 0x7A, 0xA6, 0xF9),
        ["IMM.ButtonBackgroundColor"] = MediaColor.FromArgb(0xFF, 0xF9, 0xFB, 0xFF),
        ["IMM.ButtonHoverColor"] = MediaColor.FromArgb(0xFF, 0xE7, 0xF0, 0xFF),
        ["IMM.ButtonPressedColor"] = MediaColor.FromArgb(0xFF, 0xD5, 0xE3, 0xFF),
        ["IMM.ButtonDisabledColor"] = MediaColor.FromArgb(0xFF, 0xEE, 0xF1, 0xF6),
        ["IMM.ForegroundColor"] = MediaColor.FromArgb(0xFF, 0x1C, 0x26, 0x35),
        ["IMM.SuccessColor"] = MediaColor.FromArgb(0xFF, 0x79, 0xC4, 0x6E),
        ["IMM.RowHoverColor"] = MediaColor.FromArgb(0xFF, 0x7A, 0xA6, 0xF9),
        ["IMM.RowSelectionColor"] = MediaColor.FromArgb(0x33, 0x4C, 0x8B, 0xF5),
    };

    private static readonly IReadOnlyDictionary<string, MediaColor> VintageThemeColors = new Dictionary<string, MediaColor>
    {
        ["IMM.AccentColor"] = MediaColor.FromArgb(0xE6, 0xA8, 0x8B, 0x6C),
        ["IMM.SurfaceColor"] = MediaColor.FromArgb(0xBF, 0x40, 0x35, 0x29),
        ["IMM.SurfaceAltColor"] = MediaColor.FromArgb(0xCC, 0x40, 0x35, 0x29),
        ["IMM.BorderColor"] = MediaColor.FromArgb(0x4D, 0x00, 0x00, 0x00),
        ["IMM.BorderHoverColor"] = MediaColor.FromArgb(0xE6, 0xA8, 0x8B, 0x6C),
        ["IMM.ButtonBackgroundColor"] = MediaColor.FromArgb(0xCC, 0x40, 0x35, 0x29),
        ["IMM.ButtonHoverColor"] = MediaColor.FromArgb(0xE6, 0xA8, 0x8B, 0x6C),
        ["IMM.ButtonPressedColor"] = MediaColor.FromArgb(0xFF, 0x40, 0x35, 0x29),
        ["IMM.ButtonDisabledColor"] = MediaColor.FromArgb(0xED, 0xB5, 0xAE, 0xA6),
        ["IMM.ForegroundColor"] = MediaColor.FromArgb(0xFF, 0xE9, 0xDD, 0xCE),
        ["IMM.SuccessColor"] = MediaColor.FromArgb(0xFF, 0x80, 0xFF, 0x80),
        ["IMM.RowHoverColor"] = MediaColor.FromArgb(0xE6, 0xA8, 0x8B, 0x6C),
        ["IMM.RowSelectionColor"] = MediaColor.FromArgb(0xCC, 0xA8, 0x8B, 0x6C),
    };

    private static readonly IReadOnlyDictionary<string, string> BrushMappings = new Dictionary<string, string>
    {
        ["IMM.AccentBrush"] = "IMM.AccentColor",
        ["IMM.SurfaceBrush"] = "IMM.SurfaceColor",
        ["IMM.SurfaceAltBrush"] = "IMM.SurfaceAltColor",
        ["IMM.BorderBrush"] = "IMM.BorderColor",
        ["IMM.BorderHoverBrush"] = "IMM.BorderHoverColor",
        ["IMM.ButtonBackgroundBrush"] = "IMM.ButtonBackgroundColor",
        ["IMM.ButtonHoverBrush"] = "IMM.ButtonHoverColor",
        ["IMM.ButtonPressedBrush"] = "IMM.ButtonPressedColor",
        ["IMM.ButtonDisabledBrush"] = "IMM.ButtonDisabledColor",
        ["IMM.ForegroundBrush"] = "IMM.ForegroundColor",
        ["IMM.SuccessBrush"] = "IMM.SuccessColor",
        ["IMM.RowHoverBrush"] = "IMM.RowHoverColor",
        ["IMM.RowSelectionBrush"] = "IMM.RowSelectionColor",
    };

    public static void ApplyTheme(bool useVintageTheme)
    {
        WpfApplication? app = WpfApplication.Current;
        if (app == null)
        {
            return;
        }

        ResourceDictionary resources = app.Resources;
        IReadOnlyDictionary<string, MediaColor> themeColors = useVintageTheme ? VintageThemeColors : ModernThemeColors;

        foreach (KeyValuePair<string, MediaColor> pair in themeColors)
        {
            UpdateColor(resources, pair.Key, pair.Value);
        }

        foreach (KeyValuePair<string, string> mapping in BrushMappings)
        {
            if (!themeColors.TryGetValue(mapping.Value, out MediaColor color))
            {
                continue;
            }

            UpdateBrush(resources, mapping.Key, color);
        }

        foreach (Window window in app.Windows)
        {
            window.InvalidateVisual();
        }
    }

    private static void UpdateColor(ResourceDictionary resources, string key, MediaColor color)
    {
        if (!resources.Contains(key))
        {
            return;
        }

        if (resources[key] is MediaColor existing && existing == color)
        {
            return;
        }

        resources[key] = color;
    }

    private static void UpdateBrush(ResourceDictionary resources, string key, MediaColor color)
    {
        if (!resources.Contains(key))
        {
            return;
        }

        if (resources[key] is SolidColorBrush brush)
        {
            if (brush.IsFrozen)
            {
                SolidColorBrush clone = brush.CloneCurrentValue();
                clone.Color = color;
                resources[key] = clone;
            }
            else if (brush.Color != color)
            {
                brush.Color = color;
            }
        }
    }
}
