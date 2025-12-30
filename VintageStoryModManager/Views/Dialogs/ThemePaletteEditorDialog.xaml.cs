using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Services;
using Color = System.Windows.Media.Color;
using DrawingColor = System.Drawing.Color;
using FormsColorDialog = System.Windows.Forms.ColorDialog;
using FormsDialogResult = System.Windows.Forms.DialogResult;
using FormsIWin32Window = System.Windows.Forms.IWin32Window;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views.Dialogs;

public partial class ThemePaletteEditorDialog : Window, INotifyPropertyChanged
{
    private readonly UserConfigurationService _configuration;
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _themePaletteSnapshots =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _isInitializing;
    private ThemeOption? _selectedThemeOption;

    public ThemePaletteEditorDialog(UserConfigurationService configuration)
    {
        InitializeComponent();
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        PaletteItems = new ObservableCollection<PaletteColorEntry>();
        ThemeOptions = new ObservableCollection<ThemeOption>();
        DataContext = this;
        _isInitializing = true;
        RefreshThemeOptions();
        CaptureThemePaletteSnapshots();
        SelectedThemeOption ??= ThemeOptions.FirstOrDefault();
        LoadPalette();
        UpdateResetAvailability();
        _isInitializing = false;
    }

    public ObservableCollection<PaletteColorEntry> PaletteItems { get; }

    public ObservableCollection<ThemeOption> ThemeOptions { get; }

    public ThemeOption? SelectedThemeOption
    {
        get => _selectedThemeOption;
        set
        {
            if (!SetProperty(ref _selectedThemeOption, value)) return;

            UpdateResetAvailability();
        }
    }

    private void CaptureThemePaletteSnapshots()
    {
        _themePaletteSnapshots.Clear();

        foreach (var name in _configuration.GetAllThemeNames())
            if (_configuration.TryGetThemePalette(name, out var palette))
                _themePaletteSnapshots[name] =
                    new Dictionary<string, string>(palette, StringComparer.OrdinalIgnoreCase);

        var currentName = _configuration.GetCurrentThemeName();
        if (!_themePaletteSnapshots.ContainsKey(currentName)
            && _configuration.TryGetThemePalette(currentName, out var currentPalette))
            _themePaletteSnapshots[currentName] =
                new Dictionary<string, string>(currentPalette, StringComparer.OrdinalIgnoreCase);
    }

    private void LoadPalette()
    {
        PaletteItems.Clear();

        foreach (var pair in _configuration.GetThemePaletteColors()
                     .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            PaletteItems.Add(new PaletteColorEntry(pair.Key, pair.Value, PickColor));
    }

    private void RefreshThemeOptions()
    {
        _isInitializing = true;
        var currentName = SelectedThemeOption?.Name ?? _configuration.GetCurrentThemeName();

        ThemeOptions.Clear();

        ThemeOptions.Add(new ThemeOption(UserConfigurationService.GetThemeDisplayName(ColorTheme.VintageStory),
            ColorTheme.VintageStory));
        ThemeOptions.Add(new ThemeOption(UserConfigurationService.GetThemeDisplayName(ColorTheme.Dark), ColorTheme.Dark));
        ThemeOptions.Add(new ThemeOption(UserConfigurationService.GetThemeDisplayName(ColorTheme.Light), ColorTheme.Light));

        foreach (var name in _configuration.GetCustomThemeNames())
            ThemeOptions.Add(new ThemeOption(name, null));

        if (_configuration.ColorTheme == ColorTheme.Custom
            && ThemeOptions.All(option => !string.Equals(option.Name, currentName, StringComparison.OrdinalIgnoreCase)))
            ThemeOptions.Add(new ThemeOption(currentName, null));

        SelectedThemeOption = ThemeOptions.FirstOrDefault(option =>
                                  string.Equals(option.Name, currentName, StringComparison.OrdinalIgnoreCase))
                              ?? SelectedThemeOption;
        _isInitializing = false;
        UpdateResetAvailability();
    }

    private void PickColor(PaletteColorEntry? entry)
    {
        if (entry is null) return;

        using var dialog = new FormsColorDialog
        {
            FullOpen = true,
            AnyColor = true
        };

        if (TryParseColor(entry.HexValue, out var currentColor))
            dialog.Color = DrawingColor.FromArgb(currentColor.A, currentColor.R, currentColor.G, currentColor.B);

        var ownerHandle = new WindowInteropHelper(this).Handle;
        var result = ownerHandle != IntPtr.Zero
            ? dialog.ShowDialog(new Win32Window(ownerHandle))
            : dialog.ShowDialog();

        if (result != FormsDialogResult.OK) return;

        var selected = dialog.Color;
        var hex = $"#{selected.A:X2}{selected.R:X2}{selected.G:X2}{selected.B:X2}";

        ApplyPaletteEntry(entry, hex);
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
        var palette = _configuration.GetThemePaletteColors();
        App.ApplyTheme(_configuration.ColorTheme, palette.Count > 0 ? palette : null);
    }

    private static bool TryParseColor(string? value, out Color color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(value)) return false;

        try
        {
            var converted = MediaColorConverter.ConvertFromString(value);
            if (converted is Color parsed)
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

    private void ApplySelectedTheme()
    {
        if (_isInitializing || SelectedThemeOption is null) return;

        if (_configuration.TryActivateTheme(SelectedThemeOption.Name))
        {
            LoadPalette();
            ApplyTheme();
        }
    }

    private void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedThemeOption?.SupportsReset != true) return;

        _configuration.TryActivateTheme(SelectedThemeOption.Name);
        _configuration.ResetThemePalette();
        LoadPalette();
        ApplyTheme();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryResolveUnsavedChanges(SelectedThemeOption, isClosing: true)) return;

        Close();
    }

    private void UpdateResetAvailability()
    {
        if (ResetButton is not null) ResetButton.IsEnabled = SelectedThemeOption?.SupportsReset == true;
    }

    private void PaletteComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || PaletteItems.Count == 0) return;

        var previousTheme = e.RemovedItems.OfType<ThemeOption>().FirstOrDefault();
        var requestedTheme = e.AddedItems.OfType<ThemeOption>().FirstOrDefault() ?? SelectedThemeOption;

        if (previousTheme is not null && !TryResolveUnsavedChanges(previousTheme))
        {
            _isInitializing = true;
            SelectedThemeOption = previousTheme;
            _isInitializing = false;
            return;
        }

        if (requestedTheme is not null && !ReferenceEquals(SelectedThemeOption, requestedTheme))
        {
            _isInitializing = true;
            SelectedThemeOption = requestedTheme;
            _isInitializing = false;
        }

        ApplySelectedTheme();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        TrySaveTheme(out _);
    }

    private void DeleteButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedThemeOption is null || !SelectedThemeOption.IsCustom) return;

        var result = WpfMessageBox.Show(
            this,
            $"Delete the theme '{SelectedThemeOption.Name}'?",
            "Simple VS Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        if (!_configuration.DeleteCustomTheme(SelectedThemeOption.Name)) return;

        _themePaletteSnapshots.Remove(SelectedThemeOption.Name);

        RefreshThemeOptions();
        SelectedThemeOption ??= ThemeOptions.FirstOrDefault();
        ApplySelectedTheme();
    }

    private bool TryResolveUnsavedChanges(ThemeOption? activeTheme, bool isClosing = false)
    {
        if (activeTheme is null || !HasUnsavedChanges(activeTheme.Name)) return true;

        var prompt = isClosing
            ? "Save changes to the current theme before closing?"
            : $"Save changes to the theme '{activeTheme.Name}' before switching?";

        var result = WpfMessageBox.Show(
            this,
            prompt,
            "Simple VS Manager",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        switch (result)
        {
            case MessageBoxResult.Yes:
                return TrySaveTheme(out _);
            case MessageBoxResult.No:
                RestoreThemeFromSnapshot(activeTheme);
                return true;
            default:
                return false;
        }
    }

    private bool HasUnsavedChanges(string themeName)
    {
        if (!_themePaletteSnapshots.TryGetValue(themeName, out var snapshot)) return false;

        var currentPalette = _configuration.GetThemePaletteColors();
        return !ArePalettesEqual(currentPalette, snapshot);
    }

    private void RestoreThemeFromSnapshot(ThemeOption themeOption)
    {
        if (!_themePaletteSnapshots.TryGetValue(themeOption.Name, out var palette)) return;

        var paletteCopy = new Dictionary<string, string>(palette, StringComparer.OrdinalIgnoreCase);
        var theme = themeOption.Theme ?? ColorTheme.Custom;

        _configuration.SetColorTheme(theme, paletteCopy);
        LoadPalette();
        ApplyTheme();
    }

    private bool TrySaveTheme(out string? savedThemeName)
    {
        savedThemeName = null;

        var defaultName = SelectedThemeOption?.Name ?? _configuration.GetCurrentThemeName();
        var dialog = new ThemeNameDialog(defaultName, _configuration)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true) return false;

        if (!_configuration.SaveCustomTheme(dialog.ThemeName))
        {
            WpfMessageBox.Show(this, "Please enter a valid theme name.", "Simple VS Manager", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        savedThemeName = dialog.ThemeName;
        UpdateThemeSnapshot(savedThemeName);

        RefreshThemeOptions();
        SelectedThemeOption = ThemeOptions.FirstOrDefault(option =>
            string.Equals(option.Name, dialog.ThemeName, StringComparison.OrdinalIgnoreCase));

        ApplySelectedTheme();
        return true;
    }

    private void UpdateThemeSnapshot(string themeName)
    {
        var palette = _configuration.GetThemePaletteColors();
        _themePaletteSnapshots[themeName] = new Dictionary<string, string>(palette, StringComparer.OrdinalIgnoreCase);
    }

    private static bool ArePalettesEqual(
        IReadOnlyDictionary<string, string> first,
        IReadOnlyDictionary<string, string> second)
    {
        if (ReferenceEquals(first, second)) return true;

        if (first.Count != second.Count) return false;

        foreach (var pair in first)
        {
            if (!second.TryGetValue(pair.Key, out var value)) return false;

            if (!string.Equals(pair.Value, value, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }

    public sealed class ThemeOption
    {
        public ThemeOption(string name, ColorTheme? theme)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Theme = theme;
        }

        public string Name { get; }

        public ColorTheme? Theme { get; }

        public bool IsCustom => Theme is null;

        public bool SupportsReset => Theme is ColorTheme.VintageStory or ColorTheme.Dark or ColorTheme.Light;
    }

    private sealed class Win32Window : FormsIWin32Window
    {
        public Win32Window(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle { get; }
    }
}

public sealed class PaletteColorEntry : ObservableObject
{
    private readonly Action<PaletteColorEntry> _selectColorAction;
    private string _hexValue;
    private MediaBrush _previewBrush;

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
            var converted = MediaColorConverter.ConvertFromString(hexValue);
            if (converted is Color color)
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