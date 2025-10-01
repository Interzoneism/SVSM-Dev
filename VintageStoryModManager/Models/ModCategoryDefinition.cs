using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace VintageStoryModManager.Models;

/// <summary>
/// Represents a user-defined category built from keyword and tag matchers.
/// </summary>
public sealed class ModCategoryDefinition
{
    public ModCategoryDefinition(string name, IReadOnlyList<string> keywordMatchers, IReadOnlyList<string> tagMatchers)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
        }

        Name = name.Trim();
        KeywordMatchers = new ReadOnlyCollection<string>(keywordMatchers ?? Array.Empty<string>());
        TagMatchers = new ReadOnlyCollection<string>(tagMatchers ?? Array.Empty<string>());
    }

    /// <summary>
    /// Display name for the category.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Plain text matchers that are applied to mod metadata such as id or description.
    /// </summary>
    public IReadOnlyList<string> KeywordMatchers { get; }

    /// <summary>
    /// Mod database tag matchers (prefixed with <c>%</c> in the configuration file).
    /// </summary>
    public IReadOnlyList<string> TagMatchers { get; }

    /// <summary>
    /// True when either keyword or tag matchers are present.
    /// </summary>
    public bool HasMatchers => KeywordMatchers.Count > 0 || TagMatchers.Count > 0;

    public override string ToString() => Name;
}
