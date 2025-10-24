using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Services;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views.Dialogs;

public partial class ThemePaletteEditorDialog : Window
{
    private readonly UserConfigurationService _configuration;

    public ThemePaletteEditorDialog(UserConfigurationService configuration)
    {
        InitializeComponent();
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        PaletteItems = new ObservableCollection<PaletteColorEntry>();
        DataContext = this;
        LoadPalette();
    }

    public ObservableCollection<PaletteColorEntry> PaletteItems { get; }

    private void LoadPalette()
    {
        PaletteItems.Clear();

        foreach (var pair in _configuration.GetThemePaletteColors().OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            PaletteItems.Add(new PaletteColorEntry(pair.Key, pair.Value, PickColor));
        }
    }

    private void PickColor(PaletteColorEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        var dialog = new ColorPickerDialog(entry.HexValue)
        {
            Owner = this
        };

        bool? result = dialog.ShowDialog();
        if (result != true)
        {
            return;
        }

        ApplyPaletteEntry(entry, dialog.SelectedHexValue);
    }

    private void ApplyPaletteEntry(PaletteColorEntry entry, string hexValue)
    {
        if (!_configuration.TrySetThemePaletteColor(entry.Key, hexValue))
        {
            WpfMessageBox.Show(
                this,
                "Please enter a colour in #RRGGBB or #AARRGGBB format.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        entry.UpdateFromHex(hexValue);
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        IReadOnlyDictionary<string, string> palette = _configuration.GetThemePaletteColors();
        App.ApplyTheme(_configuration.ColorTheme, palette.Count > 0 ? palette : null);
    }

    private void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        _configuration.ResetThemePalette();
        LoadPalette();
        ApplyTheme();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

}

public sealed class PaletteColorEntry : ObservableObject
{
    private readonly Action<PaletteColorEntry> _selectColorAction;
    private MediaBrush _previewBrush;
    private string _hexValue;

    public PaletteColorEntry(string key, string hexValue, Action<PaletteColorEntry> selectColorAction)
    {
        Key = key;
        _hexValue = hexValue;
        _selectColorAction = selectColorAction ?? throw new ArgumentNullException(nameof(selectColorAction));
        _previewBrush = CreateBrush(hexValue);
        SelectColorCommand = new RelayCommand(() => _selectColorAction(this));
    }

    public string Key { get; }

    public string DisplayName => Key;

    public string HexValue
    {
        get => _hexValue;
        private set => SetProperty(ref _hexValue, value);
    }

    public MediaBrush PreviewBrush
    {
        get => _previewBrush;
        private set => SetProperty(ref _previewBrush, value);
    }

    public IRelayCommand SelectColorCommand { get; }

    public void UpdateFromHex(string hexValue)
    {
        HexValue = hexValue;
        PreviewBrush = CreateBrush(hexValue);
    }

    private static MediaBrush CreateBrush(string hexValue)
    {
        try
        {
            object? converted = MediaColorConverter.ConvertFromString(hexValue);
            if (converted is MediaColor color)
            {
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
        }
        catch
        {
            // Ignore and fall through to transparent.
        }

        return MediaBrushes.Transparent;
    }
}
