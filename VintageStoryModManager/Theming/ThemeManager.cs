using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace VintageStoryModManager.Theming;

public static class ThemeManager
{
    private static readonly ThemePalette ModernPalette = CreateModernPalette();
    private static readonly ThemePalette VintagePalette = CreateVintagePalette();

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Modern;

    public static void ApplyTheme(ResourceDictionary resources, AppTheme theme)
    {
        if (resources == null)
        {
            throw new ArgumentNullException(nameof(resources));
        }

        ThemePalette palette = theme switch
        {
            AppTheme.Vintage => VintagePalette,
            _ => ModernPalette
        };

        palette.Apply(resources);
        CurrentTheme = theme;
    }

    private static ThemePalette CreateModernPalette()
    {
        return new ThemePalette(
            new Dictionary<string, MediaColor>
            {
                ["IMM.AccentColor"] = FromHex("#FF4C8BF5"),
                ["IMM.SurfaceColor"] = FromHex("#FFF6F8FB"),
                ["IMM.SurfaceAltColor"] = FromHex("#FFEFF3F9"),
                ["IMM.BorderColor"] = FromHex("#FFCAD7EB"),
                ["IMM.BorderHoverColor"] = FromHex("#FF7AA6F9"),
                ["IMM.ButtonBackgroundColor"] = FromHex("#FFF9FBFF"),
                ["IMM.ButtonHoverColor"] = FromHex("#FFE7F0FF"),
                ["IMM.ButtonPressedColor"] = FromHex("#FFD5E3FF"),
                ["IMM.ButtonDisabledColor"] = FromHex("#FFEEF1F6"),
                ["IMM.ButtonForegroundColor"] = FromHex("#FF1C2635"),
                ["IMM.ListBackgroundColor"] = FromHex("#FFFFFFFF"),
                ["IMM.ForegroundColor"] = FromHex("#FF1C2635"),
                ["IMM.SecondaryTextColor"] = FromHex("#FF617191"),
                ["IMM.SuccessColor"] = FromHex("#FF79C46E"),
                ["IMM.RowHoverColor"] = FromHex("#FF7AA6F9"),
                ["IMM.RowSelectionColor"] = FromHex("#334C8BF5"),
                ["IMM.ScrollBackgroundColor"] = FromHex("#FFEFF3F9"),
                ["IMM.ScrollThumbColor"] = FromHex("#FFCAD7EB"),
                ["IMM.InputBackgroundColor"] = FromHex("#FFFFFFFF"),
                ["IMM.InputBorderColor"] = FromHex("#FFCAD7EB"),
                ["IMM.InputForegroundColor"] = FromHex("#FF1C2635"),
                ["IMM.InputFocusBackgroundColor"] = FromHex("#FFE7F0FF"),
                ["IMM.HeaderBackgroundColor"] = FromHex("#FFEFF3F9"),
                ["IMM.HeaderTextColor"] = FromHex("#FF1C2635"),
                ["IMM.OnAccentTextColor"] = FromHex("#FFFFFFFF"),
                ["VS.DialogLightBackgroundColor"] = FromHex("#BF403529"),
                ["VS.DialogDefaultBackgroundColor"] = FromHex("#CC403529"),
                ["VS.DialogStrongBackgroundColor"] = FromHex("#FF403529"),
                ["VS.DialogBorderColor"] = FromHex("#4D000000"),
                ["VS.DialogHighlightColor"] = FromHex("#E6A88B6C"),
                ["VS.DialogAlternateBackgroundColor"] = FromHex("#EDB5AEA6"),
                ["VS.DialogDefaultTextColor"] = FromHex("#FFE9DDCE"),
                ["VS.DialogDarkBrownTextColor"] = FromHex("#FF5A4530"),
                ["VS.DialogHotbarNumberTextColor"] = FromHex("#805A4530"),
                ["VS.DialogButtonTextColor"] = FromHex("#FFE0CFBB"),
                ["VS.DialogActiveButtonTextColor"] = FromHex("#FFC58948"),
                ["VS.DialogDisabledTextColor"] = FromHex("#59FFFFFF"),
                ["VS.DialogSuccessTextColor"] = FromHex("#FF80FF80"),
                ["VS.DialogErrorTextColor"] = FromHex("#FFFF8080"),
                ["VS.DialogWarningTextColor"] = FromHex("#FFF2C983"),
                ["VS.DialogLinkTextColor"] = FromHex("#FF8080FF"),
                ["VS.DialogTitleBarColor"] = FromHex("#33000000"),
            },
            new Dictionary<string, MediaColor>
            {
                ["IMM.AccentBrush"] = FromHex("#FF4C8BF5"),
                ["IMM.SurfaceBrush"] = FromHex("#FFF6F8FB"),
                ["IMM.SurfaceAltBrush"] = FromHex("#FFEFF3F9"),
                ["IMM.BorderBrush"] = FromHex("#FFCAD7EB"),
                ["IMM.BorderHoverBrush"] = FromHex("#FF7AA6F9"),
                ["IMM.ButtonBackgroundBrush"] = FromHex("#FFF9FBFF"),
                ["IMM.ButtonHoverBrush"] = FromHex("#FFE7F0FF"),
                ["IMM.ButtonPressedBrush"] = FromHex("#FFD5E3FF"),
                ["IMM.ButtonDisabledBrush"] = FromHex("#FFEEF1F6"),
                ["IMM.ButtonForegroundBrush"] = FromHex("#FF1C2635"),
                ["IMM.ListBackgroundBrush"] = FromHex("#FFFFFFFF"),
                ["IMM.ForegroundBrush"] = FromHex("#FF1C2635"),
                ["IMM.SecondaryTextBrush"] = FromHex("#FF617191"),
                ["IMM.SuccessBrush"] = FromHex("#FF79C46E"),
                ["IMM.RowHoverBrush"] = FromHex("#FF7AA6F9"),
                ["IMM.RowSelectionBrush"] = FromHex("#334C8BF5"),
                ["IMM.ScrollBackgroundBrush"] = FromHex("#FFEFF3F9"),
                ["IMM.ScrollThumbBrush"] = FromHex("#FFCAD7EB"),
                ["IMM.InputBackgroundBrush"] = FromHex("#FFFFFFFF"),
                ["IMM.InputBorderBrush"] = FromHex("#FFCAD7EB"),
                ["IMM.InputForegroundBrush"] = FromHex("#FF1C2635"),
                ["IMM.InputFocusBackgroundBrush"] = FromHex("#FFE7F0FF"),
                ["IMM.HeaderBackgroundBrush"] = FromHex("#FFEFF3F9"),
                ["IMM.HeaderTextBrush"] = FromHex("#FF1C2635"),
                ["IMM.OnAccentTextBrush"] = FromHex("#FFFFFFFF"),
                ["VS.DialogLightBackgroundBrush"] = FromHex("#BF403529"),
                ["VS.DialogDefaultBackgroundBrush"] = FromHex("#CC403529"),
                ["VS.DialogStrongBackgroundBrush"] = FromHex("#FF403529"),
                ["VS.DialogBorderBrush"] = FromHex("#4D000000"),
                ["VS.DialogHighlightBrush"] = FromHex("#E6A88B6C"),
                ["VS.DialogAlternateBackgroundBrush"] = FromHex("#EDB5AEA6"),
                ["VS.DialogDefaultTextBrush"] = FromHex("#FFE9DDCE"),
                ["VS.DialogDarkBrownTextBrush"] = FromHex("#FF5A4530"),
                ["VS.DialogHotbarNumberTextBrush"] = FromHex("#805A4530"),
                ["VS.DialogButtonTextBrush"] = FromHex("#FFE0CFBB"),
                ["VS.DialogActiveButtonTextBrush"] = FromHex("#FFC58948"),
                ["VS.DialogDisabledTextBrush"] = FromHex("#59FFFFFF"),
                ["VS.DialogSuccessTextBrush"] = FromHex("#FF80FF80"),
                ["VS.DialogErrorTextBrush"] = FromHex("#FFFF8080"),
                ["VS.DialogWarningTextBrush"] = FromHex("#FFF2C983"),
                ["VS.DialogLinkTextBrush"] = FromHex("#FF8080FF"),
                ["VS.DialogTitleBarBrush"] = FromHex("#33000000"),
            });
    }

    private static ThemePalette CreateVintagePalette()
    {
        return new ThemePalette(
            new Dictionary<string, MediaColor>
            {
                ["IMM.AccentColor"] = FromHex("#FF8A6A44"),
                ["IMM.SurfaceColor"] = FromHex("#FF4A3B26"),
                ["IMM.SurfaceAltColor"] = FromHex("#FF5B4730"),
                ["IMM.BorderColor"] = FromHex("#FF2C1F10"),
                ["IMM.BorderHoverColor"] = FromHex("#FF8A6A44"),
                ["IMM.ButtonBackgroundColor"] = FromHex("#FF705232"),
                ["IMM.ButtonHoverColor"] = FromHex("#FF8A6A44"),
                ["IMM.ButtonPressedColor"] = FromHex("#FF7C5F3C"),
                ["IMM.ButtonDisabledColor"] = FromHex("#665B4730"),
                ["IMM.ButtonForegroundColor"] = FromHex("#FFE6D2A4"),
                ["IMM.ListBackgroundColor"] = FromHex("#FF5B4730"),
                ["IMM.ForegroundColor"] = FromHex("#FFEADCB8"),
                ["IMM.SecondaryTextColor"] = FromHex("#FFBDAA84"),
                ["IMM.SuccessColor"] = FromHex("#FF80FF80"),
                ["IMM.RowHoverColor"] = FromHex("#FF6B5436"),
                ["IMM.RowSelectionColor"] = FromHex("#807C5F3C"),
                ["IMM.ScrollBackgroundColor"] = FromHex("#FF3F3020"),
                ["IMM.ScrollThumbColor"] = FromHex("#FF7C5F3C"),
                ["IMM.InputBackgroundColor"] = FromHex("#FF3B2D1D"),
                ["IMM.InputBorderColor"] = FromHex("#FF7B6546"),
                ["IMM.InputForegroundColor"] = FromHex("#FFF3EAD3"),
                ["IMM.InputFocusBackgroundColor"] = FromHex("#FF6B5436"),
                ["IMM.HeaderBackgroundColor"] = FromHex("#FF3C2E1D"),
                ["IMM.HeaderTextColor"] = FromHex("#FFD8C69E"),
                ["IMM.OnAccentTextColor"] = FromHex("#FFF3EAD3"),
                ["VS.DialogLightBackgroundColor"] = FromHex("#BF403529"),
                ["VS.DialogDefaultBackgroundColor"] = FromHex("#CC403529"),
                ["VS.DialogStrongBackgroundColor"] = FromHex("#FF403529"),
                ["VS.DialogBorderColor"] = FromHex("#4D000000"),
                ["VS.DialogHighlightColor"] = FromHex("#E6A88B6C"),
                ["VS.DialogAlternateBackgroundColor"] = FromHex("#EDB5AEA6"),
                ["VS.DialogDefaultTextColor"] = FromHex("#FFE9DDCE"),
                ["VS.DialogDarkBrownTextColor"] = FromHex("#FF5A4530"),
                ["VS.DialogHotbarNumberTextColor"] = FromHex("#805A4530"),
                ["VS.DialogButtonTextColor"] = FromHex("#FFE0CFBB"),
                ["VS.DialogActiveButtonTextColor"] = FromHex("#FFC58948"),
                ["VS.DialogDisabledTextColor"] = FromHex("#59FFFFFF"),
                ["VS.DialogSuccessTextColor"] = FromHex("#FF80FF80"),
                ["VS.DialogErrorTextColor"] = FromHex("#FFFF8080"),
                ["VS.DialogWarningTextColor"] = FromHex("#FFF2C983"),
                ["VS.DialogLinkTextColor"] = FromHex("#FF8080FF"),
                ["VS.DialogTitleBarColor"] = FromHex("#33000000"),
            },
            new Dictionary<string, MediaColor>
            {
                ["IMM.AccentBrush"] = FromHex("#FF8A6A44"),
                ["IMM.SurfaceBrush"] = FromHex("#FF4A3B26"),
                ["IMM.SurfaceAltBrush"] = FromHex("#FF5B4730"),
                ["IMM.BorderBrush"] = FromHex("#FF2C1F10"),
                ["IMM.BorderHoverBrush"] = FromHex("#FF8A6A44"),
                ["IMM.ButtonBackgroundBrush"] = FromHex("#FF705232"),
                ["IMM.ButtonHoverBrush"] = FromHex("#FF8A6A44"),
                ["IMM.ButtonPressedBrush"] = FromHex("#FF7C5F3C"),
                ["IMM.ButtonDisabledBrush"] = FromHex("#665B4730"),
                ["IMM.ButtonForegroundBrush"] = FromHex("#FFE6D2A4"),
                ["IMM.ListBackgroundBrush"] = FromHex("#FF5B4730"),
                ["IMM.ForegroundBrush"] = FromHex("#FFEADCB8"),
                ["IMM.SecondaryTextBrush"] = FromHex("#FFBDAA84"),
                ["IMM.SuccessBrush"] = FromHex("#FF80FF80"),
                ["IMM.RowHoverBrush"] = FromHex("#FF6B5436"),
                ["IMM.RowSelectionBrush"] = FromHex("#807C5F3C"),
                ["IMM.ScrollBackgroundBrush"] = FromHex("#FF3F3020"),
                ["IMM.ScrollThumbBrush"] = FromHex("#FF7C5F3C"),
                ["IMM.InputBackgroundBrush"] = FromHex("#FF3B2D1D"),
                ["IMM.InputBorderBrush"] = FromHex("#FF7B6546"),
                ["IMM.InputForegroundBrush"] = FromHex("#FFF3EAD3"),
                ["IMM.InputFocusBackgroundBrush"] = FromHex("#FF6B5436"),
                ["IMM.HeaderBackgroundBrush"] = FromHex("#FF3C2E1D"),
                ["IMM.HeaderTextBrush"] = FromHex("#FFD8C69E"),
                ["IMM.OnAccentTextBrush"] = FromHex("#FFF3EAD3"),
                ["VS.DialogLightBackgroundBrush"] = FromHex("#BF403529"),
                ["VS.DialogDefaultBackgroundBrush"] = FromHex("#CC403529"),
                ["VS.DialogStrongBackgroundBrush"] = FromHex("#FF403529"),
                ["VS.DialogBorderBrush"] = FromHex("#4D000000"),
                ["VS.DialogHighlightBrush"] = FromHex("#E6A88B6C"),
                ["VS.DialogAlternateBackgroundBrush"] = FromHex("#EDB5AEA6"),
                ["VS.DialogDefaultTextBrush"] = FromHex("#FFE9DDCE"),
                ["VS.DialogDarkBrownTextBrush"] = FromHex("#FF5A4530"),
                ["VS.DialogHotbarNumberTextBrush"] = FromHex("#805A4530"),
                ["VS.DialogButtonTextBrush"] = FromHex("#FFE0CFBB"),
                ["VS.DialogActiveButtonTextBrush"] = FromHex("#FFC58948"),
                ["VS.DialogDisabledTextBrush"] = FromHex("#59FFFFFF"),
                ["VS.DialogSuccessTextBrush"] = FromHex("#FF80FF80"),
                ["VS.DialogErrorTextBrush"] = FromHex("#FFFF8080"),
                ["VS.DialogWarningTextBrush"] = FromHex("#FFF2C983"),
                ["VS.DialogLinkTextBrush"] = FromHex("#FF8080FF"),
                ["VS.DialogTitleBarBrush"] = FromHex("#33000000"),
            });
    }

    private static MediaColor FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            throw new ArgumentException("Hex color cannot be null or empty.", nameof(hex));
        }

        if (System.Windows.Media.ColorConverter.ConvertFromString(hex) is MediaColor color)
        {
            return color;
        }

        throw new FormatException($"Invalid color value '{hex}'.");
    }

    private sealed class ThemePalette
    {
        private readonly IReadOnlyDictionary<string, MediaColor> _colors;
        private readonly IReadOnlyDictionary<string, MediaColor> _brushColors;

        public ThemePalette(IReadOnlyDictionary<string, MediaColor> colors, IReadOnlyDictionary<string, MediaColor> brushColors)
        {
            _colors = colors;
            _brushColors = brushColors;
        }

        public void Apply(ResourceDictionary resources)
        {
            foreach ((string key, MediaColor color) in _colors)
            {
                resources[key] = color;
            }

            foreach ((string key, MediaColor color) in _brushColors)
            {
                resources[key] = new MediaSolidColorBrush(color);
            }
        }
    }
}

