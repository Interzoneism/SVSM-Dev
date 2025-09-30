using System.Collections.Generic;

namespace VintageStoryModManager.Models;

/// <summary>
/// Represents a saved set of mod activation states.
/// </summary>
/// <param name="Name">Display name for the preset.</param>
/// <param name="DisabledEntries">Collection of disabled mod identifiers stored in clientsettings.json.</param>
public sealed record ModPreset(string Name, IReadOnlyList<string> DisabledEntries);
