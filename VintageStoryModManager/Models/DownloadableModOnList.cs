using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace VintageStoryModManager.Models;

/// <summary>
/// Represents a mod listing from the Vintage Story mod database.
/// </summary>
public class DownloadableModOnList : INotifyPropertyChanged
{
    [JsonPropertyName("modid")]
    public int ModId { get; set; }

    [JsonPropertyName("assetid")]
    public int AssetId { get; set; }

    [JsonPropertyName("downloads")]
    public int Downloads { get; set; }

    [JsonPropertyName("follows")]
    public int Follows { get; set; }

    [JsonPropertyName("trendingpoints")]
    public int TrendingPoints { get; set; }

    [JsonPropertyName("comments")]
    public int Comments { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("modidstrs")]
    public List<string> ModIdStrings { get; set; } = [];

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("urlalias")]
    public string? UrlAlias { get; set; }

    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }

    [JsonPropertyName("logofiledb")]
    public string? LogoFileDatabase { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("lastreleased")]
    public string LastReleased { get; set; } = string.Empty;

    /// <summary>
    /// Default color used when a logo has not been analyzed yet or cannot be processed.
    /// </summary>
    public static readonly Color NeutralLogoColor = Color.FromRgb(26, 26, 28);

    private string _userReportDisplay = string.Empty;

    private string _userReportTooltip = "User reports require a known Vintage Story version.";

    private bool _showUserReportBadge;

    private bool _isInstalled;

    private Color _averageLogoColor = NeutralLogoColor;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets or sets the user report summary display text.
    /// </summary>
    public string UserReportDisplay
    {
        get => _userReportDisplay;
        set
        {
            if (value == _userReportDisplay) return;

            _userReportDisplay = value;
            OnPropertyChanged();
        }
    }

    public bool ShowUserReportBadge
    {
        get => _showUserReportBadge;
        set
        {
            if (value == _showUserReportBadge) return;

            _showUserReportBadge = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the tooltip describing all user report vote options.
    /// </summary>
    public string UserReportTooltip
    {
        get => _userReportTooltip;
        set
        {
            if (value == _userReportTooltip) return;

            _userReportTooltip = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets whether the mod is installed locally.
    /// </summary>
    public bool IsInstalled
    {
        get => _isInstalled;
        set
        {
            if (value == _isInstalled) return;

            _isInstalled = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the analyzed average logo color for UI theming.
    /// </summary>
    public Color AverageLogoColor
    {
        get => _averageLogoColor;
        set
        {
            if (value == _averageLogoColor) return;

            _averageLogoColor = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets a formatted download count (e.g., "10K" for 10000+).
    /// </summary>
    public string FormattedDownloads =>
        Downloads > 10000 ? $"{Downloads / 1000}K" : Downloads.ToString();

    /// <summary>
    /// Gets a formatted follows count (e.g., "10K" for 10000+).
    /// </summary>
    public string FormattedFollows =>
        Follows > 10000 ? $"{Follows / 1000}K" : Follows.ToString();

    /// <summary>
    /// Gets a formatted comments count (e.g., "10K" for 10000+).
    /// </summary>
    public string FormattedComments =>
        Comments > 10000 ? $"{Comments / 1000}K" : Comments.ToString();

    /// <summary>
    /// Gets the database-hosted logo URL.
    /// </summary>
    public string LogoUrl => LogoFileDatabase ?? Logo ?? string.Empty;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
