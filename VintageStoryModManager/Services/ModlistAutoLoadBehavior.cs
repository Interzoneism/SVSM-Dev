namespace VintageStoryModManager.Services;

/// <summary>
///     Defines how the application should handle automatic modlist loading.
/// </summary>
public enum ModlistAutoLoadBehavior
{
    /// <summary>
    ///     Prompt the user before loading the modlist.
    /// </summary>
    Prompt,

    /// <summary>
    ///     Automatically replace the current mod configuration with the modlist.
    /// </summary>
    Replace,

    /// <summary>
    ///     Automatically add the modlist to the current mod configuration.
    /// </summary>
    Add
}