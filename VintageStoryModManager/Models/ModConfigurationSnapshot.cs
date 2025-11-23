namespace VintageStoryModManager.Models;

/// <summary>
///     Represents a captured configuration file for a mod, including its file name and content.
/// </summary>
/// <param name="FileName">The name of the configuration file.</param>
/// <param name="Content">The contents of the configuration file.</param>
/// <param name="RelativePath">The relative path where the configuration file should be placed (optional).</param>
public sealed record ModConfigurationSnapshot(string FileName, string Content, string? RelativePath = null);
