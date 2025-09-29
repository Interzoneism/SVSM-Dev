using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.ViewModels;

/// <summary>
/// View model that wraps <see cref="ModEntry"/> for presentation in the UI.
/// </summary>
public sealed class ModListItemViewModel : ObservableObject
{
    private readonly Func<ModListItemViewModel, bool, Task<ActivationResult>> _activationHandler;
    private readonly IReadOnlyList<ModDependencyInfo> _dependencies;
    private readonly ModDependencyInfo? _gameDependency;
    private readonly IReadOnlyList<string> _authors;
    private readonly IReadOnlyList<string> _contributors;
    private readonly string? _description;
    private readonly string? _metadataError;
    private readonly string? _loadError;
    private readonly IReadOnlyList<string> _databaseTags;

    private bool _isActive;
    private bool _suppressState;
    private string _tooltip = string.Empty;
    private string? _activationError;
    private bool _hasActivationError;
    private string _statusText = string.Empty;
    private string _statusDetails = string.Empty;

    public ModListItemViewModel(
        ModEntry entry,
        bool isActive,
        string location,
        Func<ModListItemViewModel, bool, Task<ActivationResult>> activationHandler)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _activationHandler = activationHandler ?? throw new ArgumentNullException(nameof(activationHandler));

        ModId = entry.ModId;
        DisplayName = string.IsNullOrWhiteSpace(entry.Name) ? entry.ModId : entry.Name;
        Version = entry.Version;
        NetworkVersion = entry.NetworkVersion;
        Website = entry.Website;
        SourcePath = entry.SourcePath;
        Location = location;
        SourceKind = entry.SourceKind;
        _authors = entry.Authors;
        _contributors = entry.Contributors;
        var databaseInfo = entry.DatabaseInfo;
        _databaseTags = databaseInfo?.Tags ?? Array.Empty<string>();
        ModDatabaseAssetId = databaseInfo?.AssetId;
        ModDatabasePageUrl = databaseInfo?.ModPageUrl;
        LatestDatabaseVersion = databaseInfo?.LatestCompatibleVersion;
        _loadError = entry.LoadError;

        WebsiteUri = TryCreateHttpUri(Website);
        WebsiteFavicon = CreateFaviconImage(WebsiteUri);
        OpenWebsiteCommand = WebsiteUri != null ? new RelayCommand(() => LaunchUri(WebsiteUri)) : null;

        ModDatabasePageUri = TryCreateHttpUri(ModDatabasePageUrl);
        ModDatabasePageFavicon = CreateFaviconImage(ModDatabasePageUri);
        OpenModDatabasePageCommand = ModDatabasePageUri != null ? new RelayCommand(() => LaunchUri(ModDatabasePageUri)) : null;

        IReadOnlyList<ModDependencyInfo> dependencies = entry.Dependencies ?? Array.Empty<ModDependencyInfo>();
        _gameDependency = dependencies.FirstOrDefault(d => string.Equals(d.ModId, "game", StringComparison.OrdinalIgnoreCase))
            ?? dependencies.FirstOrDefault(d => d.IsGameOrCoreDependency);

        if (dependencies.Count == 0)
        {
            _dependencies = Array.Empty<ModDependencyInfo>();
        }
        else
        {
            ModDependencyInfo[] filtered = dependencies.Where(d => !d.IsGameOrCoreDependency).ToArray();
            _dependencies = filtered.Length == 0 ? Array.Empty<ModDependencyInfo>() : filtered;
        }
        _description = entry.Description;
        _metadataError = entry.Error;
        Side = entry.Side;
        RequiredOnClient = entry.RequiredOnClient;
        RequiredOnServer = entry.RequiredOnServer;

        Icon = CreateImage(entry.IconBytes);

        _isActive = isActive;
        HasErrors = entry.HasErrors;

        UpdateStatusFromErrors();
        UpdateTooltip();
    }

    public string ModId { get; }

    public string DisplayName { get; }

    public string? Version { get; }

    public string VersionDisplay => string.IsNullOrWhiteSpace(Version) ? "—" : Version!;

    public string? NetworkVersion { get; }

    public string GameVersionDisplay
    {
        get
        {
            if (_gameDependency is { } dependency)
            {
                string version = dependency.Version?.Trim() ?? string.Empty;
                if (version.Length > 0)
                {
                    return version;
                }
            }

            return string.IsNullOrWhiteSpace(NetworkVersion) ? "—" : NetworkVersion!;
        }
    }

    public string AuthorsDisplay => _authors.Count == 0 ? "—" : string.Join(", ", _authors);

    public string ContributorsDisplay => _contributors.Count == 0 ? "—" : string.Join(", ", _contributors);

    public string DependenciesDisplay => _dependencies.Count == 0
        ? "—"
        : string.Join(", ", _dependencies.Select(dependency => dependency.Display));

    public IReadOnlyList<string> DatabaseTags => _databaseTags;

    public string DatabaseTagsDisplay => _databaseTags.Count == 0 ? "—" : string.Join(", ", _databaseTags);

    public string? ModDatabaseAssetId { get; }

    public string? ModDatabasePageUrl { get; }

    public Uri? ModDatabasePageUri { get; }

    public bool HasModDatabasePageLink => ModDatabasePageUri != null;

    public string ModDatabasePageUrlDisplay => string.IsNullOrWhiteSpace(ModDatabasePageUrl) ? "—" : ModDatabasePageUrl!;

    public string? LatestDatabaseVersion { get; }

    public string LatestDatabaseVersionDisplay => string.IsNullOrWhiteSpace(LatestDatabaseVersion) ? "—" : LatestDatabaseVersion!;

    public string? Website { get; }

    public Uri? WebsiteUri { get; }

    public bool HasWebsiteLink => WebsiteUri != null;

    public ImageSource? WebsiteFavicon { get; }

    public ImageSource? ModDatabasePageFavicon { get; }

    public ICommand? OpenWebsiteCommand { get; }

    public ICommand? OpenModDatabasePageCommand { get; }

    public string SourcePath { get; }

    public string Location { get; }

    public ModSourceKind SourceKind { get; }

    public string SourceKindDisplay => SourceKind switch
    {
        ModSourceKind.ZipArchive => "Zip",
        ModSourceKind.Folder => "Folder",
        ModSourceKind.Assembly => "Assembly",
        ModSourceKind.SourceCode => "Source",
        _ => SourceKind.ToString()
    };

    public string? Side { get; }

    public string SideDisplay => string.IsNullOrWhiteSpace(Side) ? "—" : Side!;

    public bool? RequiredOnClient { get; }

    public string RequiredOnClientDisplay => RequiredOnClient.HasValue ? (RequiredOnClient.Value ? "Yes" : "No") : "—";

    public bool? RequiredOnServer { get; }

    public string RequiredOnServerDisplay => RequiredOnServer.HasValue ? (RequiredOnServer.Value ? "Yes" : "No") : "—";

    public ImageSource? Icon { get; }

    public bool HasErrors { get; }

    public bool HasLoadError => !string.IsNullOrWhiteSpace(_loadError);

    public bool CanToggle => !HasErrors && !HasLoadError;

    public bool HasActivationError
    {
        get => _hasActivationError;
        private set => SetProperty(ref _hasActivationError, value);
    }

    public string? ActivationError
    {
        get => _activationError;
        private set
        {
            if (SetProperty(ref _activationError, value))
            {
                HasActivationError = !string.IsNullOrWhiteSpace(value);
                UpdateStatusFromErrors();
                UpdateTooltip();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string StatusDetails
    {
        get => _statusDetails;
        private set => SetProperty(ref _statusDetails, value);
    }

    public string Tooltip
    {
        get => _tooltip;
        private set => SetProperty(ref _tooltip, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_suppressState)
            {
                SetProperty(ref _isActive, value);
                return;
            }

            if (_isActive == value)
            {
                return;
            }

            bool previous = _isActive;
            if (!SetProperty(ref _isActive, value))
            {
                return;
            }

            _ = ApplyActivationChangeAsync(previous, value);
        }
    }

    private async Task ApplyActivationChangeAsync(bool previous, bool current)
    {
        ActivationResult result;
        try
        {
            result = await _activationHandler(this, current);
        }
        catch (Exception ex)
        {
            result = new ActivationResult(false, ex.Message);
        }

        if (!result.Success)
        {
            _suppressState = true;
            try
            {
                SetProperty(ref _isActive, previous);
            }
            finally
            {
                _suppressState = false;
            }

            ActivationError = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? "Failed to update activation state."
                : result.ErrorMessage;
        }
        else
        {
            ActivationError = null;
        }
    }

    private void UpdateStatusFromErrors()
    {
        if (HasErrors)
        {
            StatusText = "Error";
            StatusDetails = _metadataError ?? "Metadata error.";
        }
        else if (HasLoadError)
        {
            StatusText = "Error";
            StatusDetails = _loadError ?? string.Empty;
        }
        else if (HasActivationError)
        {
            StatusText = "Error";
            StatusDetails = ActivationError ?? string.Empty;
        }
        else
        {
            StatusText = string.Empty;
            StatusDetails = string.Empty;
        }
    }

    private void UpdateTooltip()
    {
        if (!string.IsNullOrWhiteSpace(_description))
        {
            Tooltip = _description.Trim();
        }
        else
        {
            Tooltip = DisplayName;
        }
    }

    private static ImageSource? CreateFaviconImage(Uri? uri)
    {
        if (uri == null)
        {
            return null;
        }

        try
        {
            var builder = new UriBuilder(uri.GetLeftPart(UriPartial.Authority))
            {
                Path = "favicon.ico"
            };

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = builder.Uri;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Uri? TryCreateHttpUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? absolute) && IsSupportedScheme(absolute))
        {
            return absolute;
        }

        if (Uri.TryCreate($"https://{value}", UriKind.Absolute, out absolute) && IsSupportedScheme(absolute))
        {
            return absolute;
        }

        return null;
    }

    private static bool IsSupportedScheme(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static void LaunchUri(Uri uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            // Opening a browser is best-effort; ignore failures.
        }
    }

    private static ImageSource? CreateImage(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }

        try
        {
            using MemoryStream stream = new(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

