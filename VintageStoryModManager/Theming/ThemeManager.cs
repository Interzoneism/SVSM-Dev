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
                ["VS.DialogLightBackgroundColor"] = MediaColor.FromArgb(0xBF, 0x40, 0x35, 0x29),
                ["VS.DialogDefaultBackgroundColor"] = MediaColor.FromArgb(0xCC, 0x40, 0x35, 0x29),
                ["VS.DialogStrongBackgroundColor"] = MediaColor.FromArgb(0xFF, 0x40, 0x35, 0x29),
                ["VS.DialogBorderColor"] = MediaColor.FromArgb(0x4D, 0x00, 0x00, 0x00),
                ["VS.DialogHighlightColor"] = MediaColor.FromArgb(0xE6, 0xA8, 0x8B, 0x6C),
                ["VS.DialogAlternateBackgroundColor"] = MediaColor.FromArgb(0xED, 0xB5, 0xAE, 0xA6),
                ["VS.DialogDefaultTextColor"] = MediaColor.FromArgb(0xFF, 0xE9, 0xDD, 0xCE),
                ["VS.DialogDarkBrownTextColor"] = MediaColor.FromArgb(0xFF, 0x5A, 0x45, 0x30),
                ["VS.DialogHotbarNumberTextColor"] = MediaColor.FromArgb(0x80, 0x5A, 0x45, 0x30),
                ["VS.DialogButtonTextColor"] = MediaColor.FromArgb(0xFF, 0xE0, 0xCF, 0xBB),
                ["VS.DialogActiveButtonTextColor"] = MediaColor.FromArgb(0xFF, 0xC5, 0x89, 0x48),
                ["VS.DialogDisabledTextColor"] = MediaColor.FromArgb(0x59, 0xFF, 0xFF, 0xFF),
                ["VS.DialogSuccessTextColor"] = MediaColor.FromArgb(0xFF, 0x80, 0xFF, 0x80),
                ["VS.DialogErrorTextColor"] = MediaColor.FromArgb(0xFF, 0xFF, 0x80, 0x80),
                ["VS.DialogWarningTextColor"] = MediaColor.FromArgb(0xFF, 0xF2, 0xC9, 0x83),
                ["VS.DialogLinkTextColor"] = MediaColor.FromArgb(0xFF, 0x80, 0x80, 0xFF),
                ["VS.DialogTitleBarColor"] = MediaColor.FromArgb(0x33, 0x00, 0x00, 0x00),
            },
            new Dictionary<string, MediaColor>
            {
                ["IMM.AccentBrush"] = MediaColor.FromArgb(0xFF, 0x4C, 0x8B, 0xF5),
                ["IMM.SurfaceBrush"] = MediaColor.FromArgb(0xFF, 0xF6, 0xF8, 0xFB),
                ["IMM.SurfaceAltBrush"] = MediaColor.FromArgb(0xFF, 0xEF, 0xF3, 0xF9),
                ["IMM.BorderBrush"] = MediaColor.FromArgb(0xFF, 0xCA, 0xD7, 0xEB),
                ["IMM.BorderHoverBrush"] = MediaColor.FromArgb(0xFF, 0x7A, 0xA6, 0xF9),
                ["IMM.ButtonBackgroundBrush"] = MediaColor.FromArgb(0xFF, 0xF9, 0xFB, 0xFF),
                ["IMM.ButtonHoverBrush"] = MediaColor.FromArgb(0xFF, 0xE7, 0xF0, 0xFF),
                ["IMM.ButtonPressedBrush"] = MediaColor.FromArgb(0xFF, 0xD5, 0xE3, 0xFF),
                ["IMM.ButtonDisabledBrush"] = MediaColor.FromArgb(0xFF, 0xEE, 0xF1, 0xF6),
                ["IMM.ForegroundBrush"] = MediaColor.FromArgb(0xFF, 0x1C, 0x26, 0x35),
                ["IMM.SuccessBrush"] = MediaColor.FromArgb(0xFF, 0x79, 0xC4, 0x6E),
                ["IMM.RowHoverBrush"] = MediaColor.FromArgb(0xFF, 0x7A, 0xA6, 0xF9),
                ["IMM.RowSelectionBrush"] = MediaColor.FromArgb(0x33, 0x4C, 0x8B, 0xF5),
                ["VS.DialogLightBackgroundBrush"] = MediaColor.FromArgb(0xBF, 0x40, 0x35, 0x29),
                ["VS.DialogDefaultBackgroundBrush"] = MediaColor.FromArgb(0xCC, 0x40, 0x35, 0x29),
                ["VS.DialogStrongBackgroundBrush"] = MediaColor.FromArgb(0xFF, 0x40, 0x35, 0x29),
                ["VS.DialogBorderBrush"] = MediaColor.FromArgb(0x4D, 0x00, 0x00, 0x00),
                ["VS.DialogHighlightBrush"] = MediaColor.FromArgb(0xE6, 0xA8, 0x8B, 0x6C),
                ["VS.DialogAlternateBackgroundBrush"] = MediaColor.FromArgb(0xED, 0xB5, 0xAE, 0xA6),
                ["VS.DialogDefaultTextBrush"] = MediaColor.FromArgb(0xFF, 0xE9, 0xDD, 0xCE),
                ["VS.DialogDarkBrownTextBrush"] = MediaColor.FromArgb(0xFF, 0x5A, 0x45, 0x30),
                ["VS.DialogHotbarNumberTextBrush"] = MediaColor.FromArgb(0x80, 0x5A, 0x45, 0x30),
                ["VS.DialogButtonTextBrush"] = MediaColor.FromArgb(0xFF, 0xE0, 0xCF, 0xBB),
                ["VS.DialogActiveButtonTextBrush"] = MediaColor.FromArgb(0xFF, 0xC5, 0x89, 0x48),
                ["VS.DialogDisabledTextBrush"] = MediaColor.FromArgb(0x59, 0xFF, 0xFF, 0xFF),
                ["VS.DialogSuccessTextBrush"] = MediaColor.FromArgb(0xFF, 0x80, 0xFF, 0x80),
                ["VS.DialogErrorTextBrush"] = MediaColor.FromArgb(0xFF, 0xFF, 0x80, 0x80),
                ["VS.DialogWarningTextBrush"] = MediaColor.FromArgb(0xFF, 0xF2, 0xC9, 0x83),
                ["VS.DialogLinkTextBrush"] = MediaColor.FromArgb(0xFF, 0x80, 0x80, 0xFF),
                ["VS.DialogTitleBarBrush"] = MediaColor.FromArgb(0x33, 0x00, 0x00, 0x00),
            });
    }

    private static ThemePalette CreateVintagePalette()
    {
        return new ThemePalette(
            new Dictionary<string, MediaColor>
            {
                ["IMM.AccentColor"] = MediaColor.FromArgb(0xE6, 0xA8, 0x8B, 0x6C),
                ["IMM.SurfaceColor"] = MediaColor.FromArgb(0xCC, 0x40, 0x35, 0x29),
                ["IMM.SurfaceAltColor"] = MediaColor.FromArgb(0xBF, 0x40, 0x35, 0x29),
                ["IMM.BorderColor"] = MediaColor.FromArgb(0x4D, 0x00, 0x00, 0x00),
                ["IMM.BorderHoverColor"] = MediaColor.FromArgb(0xE6, 0xA8, 0x8B, 0x6C),
                ["IMM.ButtonBackgroundColor"] = MediaColor.FromArgb(0xED, 0xB5, 0xAE, 0xA6),
                ["IMM.ButtonHoverColor"] = MediaColor.FromArgb(0xE6, 0xA8, 0x8B, 0x6C),
                ["IMM.ButtonPressedColor"] = MediaColor.FromArgb(0xFF, 0xC5, 0x89, 0x48),
                ["IMM.ButtonDisabledColor"] = MediaColor.FromArgb(0x59, 0xFF, 0xFF, 0xFF),
                ["IMM.ForegroundColor"] = MediaColor.FromArgb(0xFF, 0xE9, 0xDD, 0xCE),
                ["IMM.SuccessColor"] = MediaColor.FromArgb(0xFF, 0x80, 0xFF, 0x80),
                ["IMM.RowHoverColor"] = MediaColor.FromArgb(0xE6, 0xA8, 0x8B, 0x6C),
                ["IMM.RowSelectionColor"] = MediaColor.FromArgb(0x66, 0xC5, 0x89, 0x48),
                ["VS.DialogLightBackgroundColor"] = MediaColor.FromArgb(0xBF, 0x40, 0x35, 0x29),
                ["VS.DialogDefaultBackgroundColor"] = MediaColor.FromArgb(0xCC, 0x40, 0x35, 0x29),
                ["VS.DialogStrongBackgroundColor"] = MediaColor.FromArgb(0xFF, 0x40, 0x35, 0x29),
                ["VS.DialogBorderColor"] = MediaColor.FromArgb(0x4D, 0x00, 0x00, 0x00),
                ["VS.DialogHighlightColor"] = MediaColor.FromArgb(0xE6, 0xA8, 0x8B, 0x6C),
                ["VS.DialogAlternateBackgroundColor"] = MediaColor.FromArgb(0xED, 0xB5, 0xAE, 0xA6),
                ["VS.DialogDefaultTextColor"] = MediaColor.FromArgb(0xFF, 0xE9, 0xDD, 0xCE),
                ["VS.DialogDarkBrownTextColor"] = MediaColor.FromArgb(0xFF, 0x5A, 0x45, 0x30),
                ["VS.DialogHotbarNumberTextColor"] = MediaColor.FromArgb(0x80, 0x5A, 0x45, 0x30),
                ["VS.DialogButtonTextColor"] = MediaColor.FromArgb(0xFF, 0xE0, 0xCF, 0xBB),
                ["VS.DialogActiveButtonTextColor"] = MediaColor.FromArgb(0xFF, 0xC5, 0x89, 0x48),
                ["VS.DialogDisabledTextColor"] = MediaColor.FromArgb(0x59, 0xFF, 0xFF, 0xFF),
                ["VS.DialogSuccessTextColor"] = MediaColor.FromArgb(0xFF, 0x80, 0xFF, 0x80),
                ["VS.DialogErrorTextColor"] = MediaColor.FromArgb(0xFF, 0xFF, 0x80, 0x80),
                ["VS.DialogWarningTextColor"] = MediaColor.FromArgb(0xFF, 0xF2, 0xC9, 0x83),
                ["VS.DialogLinkTextColor"] = MediaColor.FromArgb(0xFF, 0x80, 0x80, 0xFF),
                ["VS.DialogTitleBarColor"] = MediaColor.FromArgb(0x33, 0x00, 0x00, 0x00),
            },
            new Dictionary<string, MediaColor>
            {
                ["IMM.AccentBrush"] = MediaColor.FromArgb(0xE6, 0xA8, 0x8B, 0x6C),
                ["IMM.SurfaceBrush"] = MediaColor.FromArgb(0xCC, 0x40, 0x35, 0x29),
                ["IMM.SurfaceAltBrush"] = MediaColor.FromArgb(0xBF, 0x40, 0x35, 0x29),
                ["IMM.BorderBrush"] = MediaColor.FromArgb(0x4D, 0x00, 0x00, 0x00),
                ["IMM.BorderHoverBrush"] = MediaColor.FromArgb(0xE6, 0xA8, 0x8B, 0x6C),
                ["IMM.ButtonBackgroundBrush"] = MediaColor.FromArgb(0xED, 0xB5, 0xAE, 0xA6),
                ["IMM.ButtonHoverBrush"] = MediaColor.FromArgb(0xE6, 0xA8, 0x8B, 0x6C),
                ["IMM.ButtonPressedBrush"] = MediaColor.FromArgb(0xFF, 0xC5, 0x89, 0x48),
                ["IMM.ButtonDisabledBrush"] = MediaColor.FromArgb(0x59, 0xFF, 0xFF, 0xFF),
                ["IMM.ForegroundBrush"] = MediaColor.FromArgb(0xFF, 0xE9, 0xDD, 0xCE),
                ["IMM.SuccessBrush"] = MediaColor.FromArgb(0xFF, 0x80, 0xFF, 0x80),
                ["IMM.RowHoverBrush"] = MediaColor.FromArgb(0xE6, 0xA8, 0x8B, 0x6C),
                ["IMM.RowSelectionBrush"] = MediaColor.FromArgb(0x66, 0xC5, 0x89, 0x48),
                ["VS.DialogLightBackgroundBrush"] = MediaColor.FromArgb(0xBF, 0x40, 0x35, 0x29),
                ["VS.DialogDefaultBackgroundBrush"] = MediaColor.FromArgb(0xCC, 0x40, 0x35, 0x29),
                ["VS.DialogStrongBackgroundBrush"] = MediaColor.FromArgb(0xFF, 0x40, 0x35, 0x29),
                ["VS.DialogBorderBrush"] = MediaColor.FromArgb(0x4D, 0x00, 0x00, 0x00),
                ["VS.DialogHighlightBrush"] = MediaColor.FromArgb(0xE6, 0xA8, 0x8B, 0x6C),
                ["VS.DialogAlternateBackgroundBrush"] = MediaColor.FromArgb(0xED, 0xB5, 0xAE, 0xA6),
                ["VS.DialogDefaultTextBrush"] = MediaColor.FromArgb(0xFF, 0xE9, 0xDD, 0xCE),
                ["VS.DialogDarkBrownTextBrush"] = MediaColor.FromArgb(0xFF, 0x5A, 0x45, 0x30),
                ["VS.DialogHotbarNumberTextBrush"] = MediaColor.FromArgb(0x80, 0x5A, 0x45, 0x30),
                ["VS.DialogButtonTextBrush"] = MediaColor.FromArgb(0xFF, 0xE0, 0xCF, 0xBB),
                ["VS.DialogActiveButtonTextBrush"] = MediaColor.FromArgb(0xFF, 0xC5, 0x89, 0x48),
                ["VS.DialogDisabledTextBrush"] = MediaColor.FromArgb(0x59, 0xFF, 0xFF, 0xFF),
                ["VS.DialogSuccessTextBrush"] = MediaColor.FromArgb(0xFF, 0x80, 0xFF, 0x80),
                ["VS.DialogErrorTextBrush"] = MediaColor.FromArgb(0xFF, 0xFF, 0x80, 0x80),
                ["VS.DialogWarningTextBrush"] = MediaColor.FromArgb(0xFF, 0xF2, 0xC9, 0x83),
                ["VS.DialogLinkTextBrush"] = MediaColor.FromArgb(0xFF, 0x80, 0x80, 0xFF),
                ["VS.DialogTitleBarBrush"] = MediaColor.FromArgb(0x33, 0x00, 0x00, 0x00),
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

