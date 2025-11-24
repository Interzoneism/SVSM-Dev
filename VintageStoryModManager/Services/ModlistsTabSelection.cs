namespace VintageStoryModManager.Services;

/// <summary>
///     Defines which modlists tab is currently selected.
/// </summary>
public enum ModlistsTabSelection
{
    /// <summary>
    ///     The local modlists tab, showing modlists stored on disk.
    /// </summary>
    Local,

    /// <summary>
    ///     The online modlists tab, showing cloud-stored modlists.
    /// </summary>
    Online
}
