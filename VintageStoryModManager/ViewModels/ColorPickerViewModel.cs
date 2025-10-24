using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace VintageStoryModManager.ViewModels;

public sealed class ColorPickerViewModel : ObservableObject
{
    private bool _suppressUpdates;
    private MediaColor _currentColor;
    private string _hexValue = string.Empty;
    private bool _hasHexError;
    private string _hexErrorMessage = string.Empty;
    private MediaBrush _previewBrush = MediaBrushes.Transparent;
    private int _alpha;
    private int _red;
    private int _green;
    private int _blue;
    private int _hue;
    private int _saturation;
    private int _lightness;

    public ColorPickerViewModel(string? initialHex)
    {
        if (!string.IsNullOrWhiteSpace(initialHex) && TryParseColor(initialHex, out MediaColor parsed))
        {
            SetColor(parsed);
        }
        else
        {
            SetColor(MediaColor.FromArgb(255, 255, 255, 255));
            if (!string.IsNullOrWhiteSpace(initialHex))
            {
                _suppressUpdates = true;
                HexValue = initialHex.Trim();
                _suppressUpdates = false;
                UpdateFromHex(HexValue);
            }
        }
    }

    public string HexValue
    {
        get => _hexValue;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (SetProperty(ref _hexValue, normalized) && !_suppressUpdates)
            {
                UpdateFromHex(normalized);
            }
        }
    }

    public bool HasHexError
    {
        get => _hasHexError;
        private set
        {
            if (SetProperty(ref _hasHexError, value))
            {
                OnPropertyChanged(nameof(HasValidColor));
            }
        }
    }

    public bool HasValidColor => !HasHexError;

    public string HexErrorMessage
    {
        get => _hexErrorMessage;
        private set => SetProperty(ref _hexErrorMessage, value);
    }

    public MediaBrush PreviewBrush
    {
        get => _previewBrush;
        private set => SetProperty(ref _previewBrush, value);
    }

    public int Alpha
    {
        get => _alpha;
        set
        {
            int clamped = Math.Clamp(value, 0, 255);
            if (SetProperty(ref _alpha, clamped) && !_suppressUpdates)
            {
                UpdateColorFromArgb();
            }
        }
    }

    public int Red
    {
        get => _red;
        set
        {
            int clamped = Math.Clamp(value, 0, 255);
            if (SetProperty(ref _red, clamped) && !_suppressUpdates)
            {
                UpdateColorFromArgb();
            }
        }
    }

    public int Green
    {
        get => _green;
        set
        {
            int clamped = Math.Clamp(value, 0, 255);
            if (SetProperty(ref _green, clamped) && !_suppressUpdates)
            {
                UpdateColorFromArgb();
            }
        }
    }

    public int Blue
    {
        get => _blue;
        set
        {
            int clamped = Math.Clamp(value, 0, 255);
            if (SetProperty(ref _blue, clamped) && !_suppressUpdates)
            {
                UpdateColorFromArgb();
            }
        }
    }

    public int Hue
    {
        get => _hue;
        set
        {
            int clamped = Math.Clamp(value, 0, 360);
            if (SetProperty(ref _hue, clamped) && !_suppressUpdates)
            {
                UpdateColorFromHsl();
            }
        }
    }

    public int Saturation
    {
        get => _saturation;
        set
        {
            int clamped = Math.Clamp(value, 0, 100);
            if (SetProperty(ref _saturation, clamped) && !_suppressUpdates)
            {
                UpdateColorFromHsl();
            }
        }
    }

    public int Lightness
    {
        get => _lightness;
        set
        {
            int clamped = Math.Clamp(value, 0, 100);
            if (SetProperty(ref _lightness, clamped) && !_suppressUpdates)
            {
                UpdateColorFromHsl();
            }
        }
    }

    public string NormalizedHexValue => FormatColor(_currentColor);

    private void UpdateFromHex(string hex)
    {
        if (TryParseColor(hex, out MediaColor color))
        {
            SetColor(color);
            return;
        }

        HasHexError = true;
        HexErrorMessage = string.IsNullOrWhiteSpace(hex)
            ? "Enter a hex colour value."
            : "Enter a hex colour in #RRGGBB or #AARRGGBB format.";
    }

    private void UpdateColorFromArgb()
    {
        MediaColor color = MediaColor.FromArgb((byte)Alpha, (byte)Red, (byte)Green, (byte)Blue);
        SetColor(color);
    }

    private void UpdateColorFromHsl()
    {
        double hue = Hue % 360;
        double saturation = Saturation / 100d;
        double lightness = Lightness / 100d;
        MediaColor color = ColorFromHsl((byte)Alpha, hue, saturation, lightness);
        SetColor(color);
    }

    private void SetColor(MediaColor color)
    {
        _suppressUpdates = true;
        try
        {
            _currentColor = color;
            Alpha = color.A;
            Red = color.R;
            Green = color.G;
            Blue = color.B;

            ColorToHsl(color, out double hue, out double saturation, out double lightness);
            Hue = (int)Math.Clamp(Math.Round(hue, MidpointRounding.AwayFromZero), 0, 360);
            Saturation = (int)Math.Clamp(Math.Round(saturation * 100, MidpointRounding.AwayFromZero), 0, 100);
            Lightness = (int)Math.Clamp(Math.Round(lightness * 100, MidpointRounding.AwayFromZero), 0, 100);

            HexValue = FormatColor(color);
        }
        finally
        {
            _suppressUpdates = false;
        }

        PreviewBrush = CreateBrush(color);
        HasHexError = false;
        HexErrorMessage = string.Empty;
        OnPropertyChanged(nameof(NormalizedHexValue));
    }

    private static MediaBrush CreateBrush(MediaColor color)
    {
        var brush = new System.Windows.Media.SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static bool TryParseColor(string? value, out MediaColor color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            object? converted = MediaColorConverter.ConvertFromString(value);
            if (converted is MediaColor parsed)
            {
                color = parsed;
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static string FormatColor(MediaColor color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static void ColorToHsl(MediaColor color, out double hue, out double saturation, out double lightness)
    {
        double r = color.R / 255d;
        double g = color.G / 255d;
        double b = color.B / 255d;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        lightness = (max + min) / 2d;

        if (delta < double.Epsilon)
        {
            hue = 0;
            saturation = 0;
            return;
        }

        saturation = lightness > 0.5 ? delta / (2d - max - min) : delta / (max + min);

        if (Math.Abs(max - r) < double.Epsilon)
        {
            hue = ((g - b) / delta + (g < b ? 6d : 0d)) / 6d;
        }
        else if (Math.Abs(max - g) < double.Epsilon)
        {
            hue = ((b - r) / delta + 2d) / 6d;
        }
        else
        {
            hue = ((r - g) / delta + 4d) / 6d;
        }

        hue *= 360d;
    }

    private static MediaColor ColorFromHsl(byte alpha, double hueDegrees, double saturation, double lightness)
    {
        double h = (hueDegrees % 360d) / 360d;
        double s = Math.Clamp(saturation, 0d, 1d);
        double l = Math.Clamp(lightness, 0d, 1d);

        double r;
        double g;
        double b;

        if (s < double.Epsilon)
        {
            r = g = b = l;
        }
        else
        {
            double q = l < 0.5 ? l * (1d + s) : l + s - l * s;
            double p = 2d * l - q;
            r = HueToRgb(p, q, h + 1d / 3d);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1d / 3d);
        }

        byte red = (byte)Math.Clamp(Math.Round(r * 255d, MidpointRounding.AwayFromZero), 0, 255);
        byte green = (byte)Math.Clamp(Math.Round(g * 255d, MidpointRounding.AwayFromZero), 0, 255);
        byte blue = (byte)Math.Clamp(Math.Round(b * 255d, MidpointRounding.AwayFromZero), 0, 255);

        return MediaColor.FromArgb(alpha, red, green, blue);
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0d)
        {
            t += 1d;
        }
        if (t > 1d)
        {
            t -= 1d;
        }
        if (t < 1d / 6d)
        {
            return p + (q - p) * 6d * t;
        }
        if (t < 1d / 2d)
        {
            return q;
        }
        if (t < 2d / 3d)
        {
            return p + (q - p) * (2d / 3d - t) * 6d;
        }
        return p;
    }
}
