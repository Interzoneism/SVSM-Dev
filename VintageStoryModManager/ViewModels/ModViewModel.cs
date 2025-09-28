using System;
using System.Linq;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.ViewModels;

public sealed partial class ModViewModel : ObservableObject
{
    private readonly ClientSettingsStore _settingsStore;
    private readonly Action<string?> _statusReporter;
    private bool _isActive;
    private bool _suppress;

    public ModViewModel(ModEntry entry, bool isActive, ClientSettingsStore settingsStore, Action<string?> statusReporter)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _statusReporter = statusReporter ?? throw new ArgumentNullException(nameof(statusReporter));
        _isActive = isActive;
        Icon = entry.IconBytes != null ? CreateBitmap(entry.IconBytes) : null;
        Initials = CreateInitials(DisplayName);
    }

    public ModEntry Entry { get; }

    public ImageSource? Icon { get; }

    public bool HasIcon => Icon != null;

    public bool HasNoIcon => Icon == null;

    public string Initials { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(Entry.Name) ? Entry.ModId : Entry.Name;

    public string ModId => Entry.ModId;

    public string VersionDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Entry.Version))
            {
                return Entry.Version!;
            }

            if (!string.IsNullOrWhiteSpace(Entry.NetworkVersion))
            {
                return $"Network {Entry.NetworkVersion}";
            }

            return "Unknown";
        }
    }

    public string AuthorsDisplay => (Entry.Authors.Count > 0 ? string.Join(", ", Entry.Authors) : "Unknown");

    public string ContributorsDisplay => (Entry.Contributors.Count > 0 ? string.Join(", ", Entry.Contributors) : "None");

    public string Description => string.IsNullOrWhiteSpace(Entry.Description) ? "No description provided." : Entry.Description!;

    public string SourceHint => Entry.SourceKind switch
    {
        ModSourceKind.ZipArchive => "Zip archive",
        ModSourceKind.SourceCode => "Source files",
        ModSourceKind.Assembly => "Assembly",
        _ => "Folder"
    };

    public string StatusText => HasError ? "Error" : (IsActive ? "Active" : "Inactive");

    public string SideDisplay => string.IsNullOrWhiteSpace(Entry.Side) ? "Any" : Entry.Side!;

    public string RequirementDisplay
    {
        get
        {
            bool client = Entry.RequiredOnClient ?? false;
            bool server = Entry.RequiredOnServer ?? false;

            if (client && server)
            {
                return "Client & Server";
            }

            if (client)
            {
                return "Client";
            }

            if (server)
            {
                return "Server";
            }

            return "Optional";
        }
    }

    public string SourcePath => Entry.SourcePath;

    public string DependenciesDisplay => Entry.Dependencies.Count == 0
        ? "None"
        : string.Join(", ", Entry.Dependencies.Select(d => d.Display));

    public string IssuesDisplay => HasError ? Entry.Error! : "None";

    public string Tooltip
    {
        get
        {
            return $"Mod ID: {ModId}\nVersion: {VersionDisplay}\nStatus: {StatusText}\nSource: {Entry.SourcePath}\nAuthors: {AuthorsDisplay}\nContributors: {ContributorsDisplay}\nDependencies: {DependenciesDisplay}";
        }
    }

    public bool HasError => Entry.HasErrors;

    public string? ErrorMessage => Entry.Error;

    public event EventHandler? ActiveStateChanged;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (!SetProperty(ref _isActive, value) || _suppress)
            {
                return;
            }

            TryPersistActiveState(value);
        }
    }

    private void TryPersistActiveState(bool requestedState)
    {
        string? versionKey = !string.IsNullOrWhiteSpace(Entry.Version) ? Entry.Version : (!string.IsNullOrWhiteSpace(Entry.NetworkVersion) ? Entry.NetworkVersion : null);
        if (!_settingsStore.TrySetActive(ModId, versionKey, requestedState, out string? error))
        {
            _suppress = true;
            SetProperty(ref _isActive, !requestedState);
            _suppress = false;
            _statusReporter.Invoke($"Failed to update '{DisplayName}': {error}");
            return;
        }

        OnPropertyChanged(nameof(StatusText));
        ActiveStateChanged?.Invoke(this, EventArgs.Empty);
        string action = requestedState ? "enabled" : "disabled";
        _statusReporter.Invoke($"{DisplayName} {action}.");
    }

    private static ImageSource? CreateBitmap(byte[] data)
    {
        try
        {
            using var stream = new MemoryStream(data, writable: false);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static string CreateInitials(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "?";
        }

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpperInvariant() : parts[0][0].ToString().ToUpperInvariant();
        }

        return string.Concat(parts[0][0], parts[1][0]).ToUpperInvariant();
    }
}
