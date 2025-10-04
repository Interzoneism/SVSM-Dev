using System.Collections.Generic;

namespace VintageStoryModManager.Models;

/// <summary>
/// Represents a saved set of mod activation states.
/// </summary>
/// <param name="Name">Display name for the preset.</param>
/// <param name="DisabledEntries">Collection of disabled mod identifiers stored in clientsettings.json.</param>
/// <param name="ModStates">Optional snapshot of per-mod state used by advanced presets.</param>
public sealed record ModPreset(
    string Name,
    IReadOnlyList<string> DisabledEntries,
    IReadOnlyList<ModPresetModState> ModStates)
{
    /// <summary>
    /// Gets a value indicating whether the preset was saved in advanced mode.
    /// </summary>
    public bool IsAdvanced => ModStates.Count > 0;
}
