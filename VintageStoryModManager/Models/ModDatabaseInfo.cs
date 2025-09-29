using System;
using System.Collections.Generic;

namespace VintageStoryModManager.Models;

/// <summary>
/// Represents metadata retrieved from the official Vintage Story mod database.
/// </summary>
public sealed class ModDatabaseInfo
{
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public string? AssetId { get; init; }

    public string? ModPageUrl { get; init; }

    public string? LatestCompatibleVersion { get; init; }

    public IReadOnlyList<string> RequiredGameVersions { get; init; } = Array.Empty<string>();
}
