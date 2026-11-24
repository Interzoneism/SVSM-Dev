using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Views.Dialogs;

public partial class ModlistDetailsOverlay : System.Windows.Controls.UserControl, INotifyPropertyChanged
{
    private string _loadingMessage = string.Empty;
    private bool _isLoading;

    public ModlistDetailsOverlay()
    {
        InitializeComponent();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CloseRequested;
    public event EventHandler? HideModlistRequested;
    public event EventHandler? HideAuthorRequested;

    public ObservableCollection<ModlistDetailsModItem> Mods { get; } = [];

    public string Title { get; private set; } = string.Empty;
    public string Subtitle { get; private set; } = string.Empty;
    public string Uploader { get; private set; } = "—";
    public string ModlistVersion { get; private set; } = "—";
    public string GameVersion { get; private set; } = "—";
    public string ModCount { get; private set; } = "0";
    public string DateDisplay { get; private set; } = "—";
    public string SubscriberCount { get; private set; } = "0";
    public string Description { get; private set; } = "No description was provided.";
    public Visibility OnlineOnlyVisibility { get; private set; } = Visibility.Collapsed;
    public Visibility LoadingVisibility => _isLoading ? Visibility.Visible : Visibility.Collapsed;
    public string LoadingMessage => _loadingMessage;

    public void ShowDetails(ModlistDetailsPresentation details)
    {
        ArgumentNullException.ThrowIfNull(details);

        Title = details.Title;
        Subtitle = details.IsOnline ? "Online modlist" : details.IsGroup ? "Local modlist group" : "Local modlist";
        Uploader = DisplayOrDash(details.Uploader);
        ModlistVersion = DisplayOrDash(details.ModlistVersion);
        GameVersion = DisplayOrDash(details.GameVersion);
        ModCount = details.Mods.Count.ToString(CultureInfo.CurrentCulture);
        DateDisplay = DisplayOrDash(details.DateDisplay);
        SubscriberCount = details.SubscriberCount.ToString(CultureInfo.CurrentCulture);
        Description = string.IsNullOrWhiteSpace(details.Description)
            ? "No description was provided."
            : details.Description.Trim();
        OnlineOnlyVisibility = details.IsOnline ? Visibility.Visible : Visibility.Collapsed;

        Mods.Clear();
        foreach (var mod in details.Mods)
            Mods.Add(new ModlistDetailsModItem(mod.ModId, mod.Version));

        SetLoading(details.Mods.Count > 0, details.Mods.Count > 0 ? "Loading mod details…" : "This modlist has no mods.");
        NotifyPresentationChanged();
        Visibility = Visibility.Visible;
        Focus();
    }

    public void SetLoading(bool isLoading, string message)
    {
        _isLoading = isLoading;
        _loadingMessage = message;
        OnPropertyChanged(nameof(LoadingVisibility));
        OnPropertyChanged(nameof(LoadingMessage));
    }

    public void Close()
    {
        Visibility = Visibility.Collapsed;
        SetLoading(false, string.Empty);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyPresentationChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(Uploader));
        OnPropertyChanged(nameof(ModlistVersion));
        OnPropertyChanged(nameof(GameVersion));
        OnPropertyChanged(nameof(ModCount));
        OnPropertyChanged(nameof(DateDisplay));
        OnPropertyChanged(nameof(SubscriberCount));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(OnlineOnlyVisibility));
    }

    private static string DisplayOrDash(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
        e.Handled = true;
    }

    private void Backdrop_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Close();
        e.Handled = true;
    }

    private void DialogSurface_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void OverlayRoot_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Close();
        e.Handled = true;
    }

    private void HideModlistButton_OnClick(object sender, RoutedEventArgs e)
    {
        HideModlistRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void HideAuthorButton_OnClick(object sender, RoutedEventArgs e)
    {
        HideAuthorRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void ModCard_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ModlistDetailsModItem { ModPageUrl: { Length: > 0 } url } }) return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Opening the browser is a convenience action; leave the details view open on failure.
        }

        e.Handled = true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record ModlistDetailsPresentation(
    string Title,
    string? Description,
    string? Uploader,
    string? ModlistVersion,
    string? GameVersion,
    string? DateDisplay,
    int SubscriberCount,
    bool IsOnline,
    bool IsGroup,
    IReadOnlyList<ModlistDetailsModReference> Mods);

public sealed record ModlistDetailsModReference(string ModId, string? Version);

public sealed class ModlistDetailsModItem : INotifyPropertyChanged
{
    private string _author = "Looking up Mod Database…";
    private string _downloadsDisplay = string.Empty;
    private string _logoUrl = string.Empty;
    private string _name;
    private string _statusText = "Loading details…";

    public ModlistDetailsModItem(string modId, string? requestedVersion)
    {
        ModId = modId;
        RequestedVersion = string.IsNullOrWhiteSpace(requestedVersion) ? "Latest" : requestedVersion.Trim();
        _name = modId;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ModId { get; }
    public string RequestedVersion { get; }
    public string Name { get => _name; private set => SetField(ref _name, value); }
    public string Author { get => _author; private set => SetField(ref _author, value); }
    public string LogoUrl { get => _logoUrl; private set => SetField(ref _logoUrl, value); }
    public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }
    public string DownloadsDisplay { get => _downloadsDisplay; private set => SetField(ref _downloadsDisplay, value); }
    public string? ModPageUrl { get; private set; }

    public void ApplyDetails(DownloadableMod details)
    {
        Name = string.IsNullOrWhiteSpace(details.Name) ? ModId : details.Name;
        Author = string.IsNullOrWhiteSpace(details.Author) ? "Unknown author" : details.Author;
        LogoUrl = details.LogoFileDatabase ?? string.Empty;
        StatusText = BuildStatus(details.Side, details.Type);
        DownloadsDisplay = details.Downloads > 0
            ? $"{FormatMetric(details.Downloads)} downloads"
            : string.Empty;
        ModPageUrl = details.AssetId > 0
            ? $"https://mods.vintagestory.at/show/mod/{details.AssetId}"
            : null;
    }

    public void MarkUnavailable()
    {
        Author = "Mod Database entry unavailable";
        StatusText = "Saved ID: " + ModId;
    }

    private static string BuildStatus(string? side, string? type)
    {
        var values = new[] { side, type }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();
        return values.Length == 0 ? "Mod Database" : string.Join(" · ", values);
    }

    private static string FormatMetric(int value)
    {
        return value switch
        {
            >= 1_000_000 => $"{value / 1_000_000d:0.#}M",
            >= 10_000 => $"{value / 1_000d:0.#}K",
            _ => value.ToString("N0", CultureInfo.CurrentCulture)
        };
    }

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (string.Equals(field, value, StringComparison.Ordinal)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
