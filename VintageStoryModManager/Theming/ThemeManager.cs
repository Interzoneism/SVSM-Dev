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

    private static MediaColor FromHex(string hex) =>
        (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;

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
                ["IMM.ButtonTextColor"] = FromHex("#FF1C2635"),
                ["IMM.ForegroundColor"] = FromHex("#FF1C2635"),
                ["IMM.ForegroundDimColor"] = FromHex("#FF5E6D84"),
                ["IMM.SuccessColor"] = FromHex("#FF79C46E"),
                ["IMM.RowHoverColor"] = FromHex("#FF7AA6F9"),
                ["IMM.RowSelectionColor"] = FromHex("#334C8BF5"),
                ["IMM.InputBackgroundColor"] = FromHex("#FFF9FBFF"),
                ["IMM.InputBorderColor"] = FromHex("#FFCAD7EB"),
                ["IMM.InputTextColor"] = FromHex("#FF1C2635"),
                ["IMM.HeaderBackgroundColor"] = FromHex("#FFEFF3F9"),
                ["IMM.HeaderTextColor"] = FromHex("#FF1C2635"),
                ["IMM.ScrollBarBackgroundColor"] = FromHex("#FFEFF3F9"),
                ["IMM.ScrollBarThumbColor"] = FromHex("#FFCAD7EB"),
                ["IMM.ErrorBackgroundColor"] = FromHex("#FFFFE5E5"),
                ["IMM.WarningBackgroundColor"] = FromHex("#22F5D67B"),
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
                ["IMM.ButtonTextBrush"] = FromHex("#FF1C2635"),
                ["IMM.ForegroundBrush"] = FromHex("#FF1C2635"),
                ["IMM.ForegroundDimBrush"] = FromHex("#FF5E6D84"),
                ["IMM.SuccessBrush"] = FromHex("#FF79C46E"),
                ["IMM.RowHoverBrush"] = FromHex("#FF7AA6F9"),
                ["IMM.RowSelectionBrush"] = FromHex("#334C8BF5"),
                ["IMM.InputBackgroundBrush"] = FromHex("#FFF9FBFF"),
                ["IMM.InputBorderBrush"] = FromHex("#FFCAD7EB"),
                ["IMM.InputTextBrush"] = FromHex("#FF1C2635"),
                ["IMM.HeaderBackgroundBrush"] = FromHex("#FFEFF3F9"),
                ["IMM.HeaderTextBrush"] = FromHex("#FF1C2635"),
                ["IMM.ScrollBarBackgroundBrush"] = FromHex("#FFEFF3F9"),
                ["IMM.ScrollBarThumbBrush"] = FromHex("#FFCAD7EB"),
                ["IMM.ErrorBackgroundBrush"] = FromHex("#FFFFE5E5"),
                ["IMM.WarningBackgroundBrush"] = FromHex("#22F5D67B"),
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
                ["IMM.BorderHoverColor"] = FromHex("#FF7B6546"),
                ["IMM.ButtonBackgroundColor"] = FromHex("#FF705232"),
                ["IMM.ButtonHoverColor"] = FromHex("#FF8A6A44"),
                ["IMM.ButtonPressedColor"] = FromHex("#FF7C5F3C"),
                ["IMM.ButtonDisabledColor"] = FromHex("#FF3F3020"),
                ["IMM.ButtonTextColor"] = FromHex("#FFE6D2A4"),
                ["IMM.ForegroundColor"] = FromHex("#FFEADCB8"),
                ["IMM.ForegroundDimColor"] = FromHex("#FFBDAA84"),
                ["IMM.SuccessColor"] = FromHex("#FF8A6A44"),
                ["IMM.RowHoverColor"] = FromHex("#FF6B5436"),
                ["IMM.RowSelectionColor"] = FromHex("#FF7C5F3C"),
                ["IMM.InputBackgroundColor"] = FromHex("#FF3B2D1D"),
                ["IMM.InputBorderColor"] = FromHex("#FF7B6546"),
                ["IMM.InputTextColor"] = FromHex("#FFF3EAD3"),
                ["IMM.HeaderBackgroundColor"] = FromHex("#FF3C2E1D"),
                ["IMM.HeaderTextColor"] = FromHex("#FFD8C69E"),
                ["IMM.ScrollBarBackgroundColor"] = FromHex("#FF3F3020"),
                ["IMM.ScrollBarThumbColor"] = FromHex("#FF7C5F3C"),
                ["IMM.ErrorBackgroundColor"] = FromHex("#668A6A44"),
                ["IMM.WarningBackgroundColor"] = FromHex("#33D8C69E"),
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
                ["IMM.BorderHoverBrush"] = FromHex("#FF7B6546"),
                ["IMM.ButtonBackgroundBrush"] = FromHex("#FF705232"),
                ["IMM.ButtonHoverBrush"] = FromHex("#FF8A6A44"),
                ["IMM.ButtonPressedBrush"] = FromHex("#FF7C5F3C"),
                ["IMM.ButtonDisabledBrush"] = FromHex("#FF3F3020"),
                ["IMM.ButtonTextBrush"] = FromHex("#FFE6D2A4"),
                ["IMM.ForegroundBrush"] = FromHex("#FFEADCB8"),
                ["IMM.ForegroundDimBrush"] = FromHex("#FFBDAA84"),
                ["IMM.SuccessBrush"] = FromHex("#FF8A6A44"),
                ["IMM.RowHoverBrush"] = FromHex("#FF6B5436"),
                ["IMM.RowSelectionBrush"] = FromHex("#FF7C5F3C"),
                ["IMM.InputBackgroundBrush"] = FromHex("#FF3B2D1D"),
                ["IMM.InputBorderBrush"] = FromHex("#FF7B6546"),
                ["IMM.InputTextBrush"] = FromHex("#FFF3EAD3"),
                ["IMM.HeaderBackgroundBrush"] = FromHex("#FF3C2E1D"),
                ["IMM.HeaderTextBrush"] = FromHex("#FFD8C69E"),
                ["IMM.ScrollBarBackgroundBrush"] = FromHex("#FF3F3020"),
                ["IMM.ScrollBarThumbBrush"] = FromHex("#FF7C5F3C"),
                ["IMM.ErrorBackgroundBrush"] = FromHex("#668A6A44"),
                ["IMM.WarningBackgroundBrush"] = FromHex("#33D8C69E"),
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

