using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using VintageStoryModManager.Models;
using VintageStoryModManager.Utilities;

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

    private bool _isActive;
    private bool _suppressState;
    private string _tooltip = string.Empty;
    private string? _activationError;
    private bool _hasActivationError;
    private string _statusText = string.Empty;
    private Uri? _officialModDbUri;

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
        Website = NormalizeWebsite(entry.Website);
        SourcePath = entry.SourcePath;
        Location = location;
        SourceKind = entry.SourceKind;
        _authors = entry.Authors;
        _contributors = entry.Contributors;

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

        OfficialModDbUri = ModDbLinkBuilder.TryCreateEntryUri(ModId);
        InitializeOfficialModDbLinkAsync(ModId);

        Icon = CreateImage(entry.IconBytes);

        _isActive = isActive;
        HasErrors = entry.HasErrors;
        StatusText = entry.HasErrors ? entry.Error ?? "Metadata error." : string.Empty;

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

    public string? Website { get; }

    public Uri? OfficialModDbUri
    {
        get => _officialModDbUri;
        private set
        {
            if (SetProperty(ref _officialModDbUri, value))
            {
                OnPropertyChanged(nameof(OfficialModDbDisplay));
                OnPropertyChanged(nameof(HasOfficialModDbLink));
            }
        }
    }

    public string OfficialModDbDisplay => OfficialModDbUri?.ToString() ?? "—";

    public bool HasOfficialModDbLink => OfficialModDbUri != null;

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

    public bool CanToggle => !HasErrors;

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
            StatusText = _metadataError ?? "Metadata error.";
        }
        else if (HasActivationError)
        {
            StatusText = ActivationError ?? string.Empty;
        }
        else
        {
            StatusText = string.Empty;
        }
    }

    private void UpdateTooltip()
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(_description))
        {
            builder.AppendLine(_description.Trim());
        }

        if (_authors.Count > 0)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append("Author(s): ").Append(string.Join(", ", _authors));
        }

        if (_contributors.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Contributor(s): ").Append(string.Join(", ", _contributors));
        }

        if (_dependencies.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Dependencies: ").Append(string.Join(", ", _dependencies.Select(d => d.Display)));
        }

        if (!string.IsNullOrWhiteSpace(Website))
        {
            builder.AppendLine();
            builder.Append("Website: ").Append(Website);
        }

        if (OfficialModDbUri != null)
        {
            builder.AppendLine();
            builder.Append("Mod DB: ").Append(OfficialModDbDisplay);
        }

        if (!string.IsNullOrWhiteSpace(_metadataError))
        {
            builder.AppendLine();
            builder.Append("Metadata error: ").Append(_metadataError);
        }

        if (!string.IsNullOrWhiteSpace(_activationError))
        {
            builder.AppendLine();
            builder.Append("Activation error: ").Append(_activationError);
        }

        Tooltip = builder.Length == 0 ? DisplayName : builder.ToString().Trim();
    }

    private void InitializeOfficialModDbLinkAsync(string? modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                Uri? resolved = await ModDbLinkBuilder.TryFetchEntryUriAsync(modId, CancellationToken.None).ConfigureAwait(false);
                if (resolved == null || Equals(resolved, OfficialModDbUri))
                {
                    return;
                }

                await DispatchToUiThreadAsync(() => OfficialModDbUri = resolved).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation; the view model will continue using the slug-based URL.
            }
            catch
            {
                // Swallow network and parsing errors. The slug-based fallback remains available.
            }
        });
    }

    private static Task DispatchToUiThreadAsync(Action action)
    {
        if (action == null)
        {
            return Task.CompletedTask;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource<object?>();
        dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                action();
                completion.SetResult(null);
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }));

        return completion.Task;
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

    private static string? NormalizeWebsite(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri))
        {
            return uri.ToString();
        }

        return trimmed;
    }
}
