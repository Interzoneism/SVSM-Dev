using System.Globalization;

namespace VintageStoryModManager.Services;

/// <summary>
///     Represents a unique identifier for tracking mod usage across game versions.
///     Used as a dictionary key for mod usage statistics and compatibility tracking.
/// </summary>
public readonly struct ModUsageTrackingKey : IEquatable<ModUsageTrackingKey>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ModUsageTrackingKey"/> struct.
    /// </summary>
    /// <param name="modId">The unique identifier of the mod.</param>
    /// <param name="modVersion">The version of the mod.</param>
    /// <param name="gameVersion">The game version.</param>
    public ModUsageTrackingKey(string modId, string modVersion, string gameVersion)
    {
        ModId = Normalize(modId);
        ModVersion = Normalize(modVersion);
        GameVersion = Normalize(gameVersion);
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
    ///     Gets the game version.
    /// </summary>
    public string GameVersion { get; }

    /// <summary>
    ///     Gets a value indicating whether this key has all required fields populated.
    /// </summary>
    public bool IsValid => !string.IsNullOrEmpty(ModId)
                           && !string.IsNullOrEmpty(ModVersion)
                           && !string.IsNullOrEmpty(GameVersion);

    /// <inheritdoc/>
    public bool Equals(ModUsageTrackingKey other)
    {
        return string.Equals(ModId, other.ModId, StringComparison.OrdinalIgnoreCase)
               && string.Equals(ModVersion, other.ModVersion, StringComparison.OrdinalIgnoreCase)
               && string.Equals(GameVersion, other.GameVersion, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is ModUsageTrackingKey other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(ModId),
            StringComparer.OrdinalIgnoreCase.GetHashCode(ModVersion),
            StringComparer.OrdinalIgnoreCase.GetHashCode(GameVersion));
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} | {1} | {2}",
            ModId,
            ModVersion,
            GameVersion);
    }

    /// <summary>
    ///     Normalizes a string value by trimming whitespace.
    /// </summary>
    /// <param name="value">The value to normalize.</param>
    /// <returns>The normalized string, or empty string if the input is null or whitespace.</returns>
    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}