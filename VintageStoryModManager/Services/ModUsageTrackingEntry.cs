namespace VintageStoryModManager.Services;

/// <summary>
///     Represents a mod usage tracking entry for collecting user feedback on mod compatibility.
/// </summary>
public readonly struct ModUsageTrackingEntry
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ModUsageTrackingEntry"/> struct.
    /// </summary>
    /// <param name="modId">The unique identifier of the mod.</param>
    /// <param name="modVersion">The version of the mod.</param>
    /// <param name="gameVersion">The game version the mod was used with.</param>
    /// <param name="canSubmitVote">Whether the user can submit a compatibility vote for this mod.</param>
    /// <param name="hasUserVote">Whether the user has already voted on this mod's compatibility.</param>
    public ModUsageTrackingEntry(
        string? modId,
        string? modVersion,
        string? gameVersion,
        bool canSubmitVote,
        bool hasUserVote)
    {
        ModId = string.IsNullOrWhiteSpace(modId) ? string.Empty : modId.Trim();
        ModVersion = string.IsNullOrWhiteSpace(modVersion) ? string.Empty : modVersion.Trim();
        GameVersion = string.IsNullOrWhiteSpace(gameVersion) ? string.Empty : gameVersion.Trim();
        CanSubmitVote = canSubmitVote;
        HasUserVote = hasUserVote;
    }

    /// <summary>
    ///     Gets the unique identifier of the mod.
    /// </summary>
    public string ModId { get; }

    /// <summary>
    ///     Gets the version of the mod.
    /// </summary>
    public string ModVersion { get; }

    /// <summary>
    ///     Gets the game version the mod was used with.
    /// </summary>
    public string GameVersion { get; }

    /// <summary>
    ///     Gets a value indicating whether the user can submit a compatibility vote for this mod.
    /// </summary>
    public bool CanSubmitVote { get; }

    /// <summary>
    ///     Gets a value indicating whether the user has already voted on this mod's compatibility.
    /// </summary>
    public bool HasUserVote { get; }
}