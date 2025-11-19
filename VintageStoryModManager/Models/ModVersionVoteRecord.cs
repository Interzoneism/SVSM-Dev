using System.Text.Json.Serialization;

namespace VintageStoryModManager.Models;

internal sealed class ModVersionVoteRecord
{
    [JsonPropertyName("option")] public ModVersionVoteOption Option { get; set; }

    [JsonPropertyName("vintageStoryVersion")] public string? VintageStoryVersion { get; set; }

    [JsonPropertyName("updatedUtc")] public string? UpdatedUtc { get; set; }

    [JsonPropertyName("comment")] public string? Comment { get; set; }
}
