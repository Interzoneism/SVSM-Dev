using System.Text.Json.Serialization;

namespace VintageStoryModManager.Models;

/// <summary>
/// Represents a mod listing from the Vintage Story mod database.
/// </summary>
public class DownloadableModOnList
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

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("lastreleased")]
    public string LastReleased { get; set; } = string.Empty;

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
    /// Gets the logo URL or default image.
    /// </summary>
    public string LogoUrl =>
        string.IsNullOrEmpty(Logo)
            ? "https://mods.vintagestory.at/web/img/mod-default.png"
            : Logo;
}
