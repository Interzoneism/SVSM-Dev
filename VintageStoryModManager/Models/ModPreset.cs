using System.Collections.Generic;

namespace VintageStoryModManager.Models;

/// <summary>
/// Represents a saved set of mod activation states.
/// </summary>
/// <param name="Name">Display name for the preset.</param>
/// <param name="DisabledEntries">Collection of disabled mod identifiers stored in clientsettings.json.</param>
/// <param name="ModStates">Optional snapshot of per-mod state captured when the preset was saved.</param>
/// <param name="IncludesModStatus">Indicates whether the preset recorded activation state for mods.</param>
/// <param name="IncludesModVersions">Indicates whether the preset recorded specific mod versions.</param>
/// <param name="IsExclusive">Indicates whether loading the preset should remove mods that were not saved.</param>
/// <param name="IncludesConfigs">Indicates whether the preset bundled mod configuration files.</param>
/// <param name="ConfigFiles">Configuration files captured when the preset or modlist was saved.</param>
public sealed record ModPreset(
    string Name,
    IReadOnlyList<string> DisabledEntries,
    IReadOnlyList<ModPresetModState> ModStates,
    bool IncludesModStatus,
    bool IncludesModVersions,
    bool IsExclusive,
    bool IncludesConfigs,
    IReadOnlyList<ModPresetConfigFile> ConfigFiles)
{
    /// <summary>
    /// Gets a value indicating whether the preset contains any recorded mod states.
    /// </summary>
    public bool HasModStates => ModStates.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the preset contains any configuration files.
    /// </summary>
    public bool HasConfigFiles => ConfigFiles.Count > 0;
}
