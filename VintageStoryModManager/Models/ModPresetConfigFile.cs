using System;

namespace VintageStoryModManager.Models;

/// <summary>
/// Represents a configuration file packaged inside a mod preset or modlist snapshot.
/// </summary>
/// <param name="ModId">The mod identifier the configuration belongs to.</param>
/// <param name="RelativePath">The path of the configuration file relative to the Vintagestory data directory.</param>
/// <param name="Contents">The raw bytes of the configuration file.</param>
public sealed record ModPresetConfigFile(string ModId, string RelativePath, ReadOnlyMemory<byte> Contents);
