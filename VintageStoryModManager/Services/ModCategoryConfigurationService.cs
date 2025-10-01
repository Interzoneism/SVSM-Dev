using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
/// Reads user-defined category configuration files from the mod configuration folder.
/// </summary>
public sealed class ModCategoryConfigurationService
{
    private static readonly char[] TrimCharacters = new[] { ' ', '\t', '\r', '\n' };

    /// <summary>
    /// Loads category definitions from the supplied data directory. When the configuration file does not exist an empty
    /// collection is returned.
    /// </summary>
    /// <param name="dataDirectory">The Vintage Story data directory that contains the <c>ModConfig</c> folder.</param>
    public IReadOnlyList<ModCategoryDefinition> LoadCategories(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            return Array.Empty<ModCategoryDefinition>();
        }

        string configPath;
        try
        {
            string normalized = Path.GetFullPath(dataDirectory);
            configPath = Path.Combine(normalized, "ModConfig", "ImprovedModMenu", "categories.txt");
        }
        catch (Exception)
        {
            return Array.Empty<ModCategoryDefinition>();
        }

        if (!File.Exists(configPath))
        {
            return Array.Empty<ModCategoryDefinition>();
        }

        try
        {
            using var reader = new StreamReader(configPath);
            return ParseDefinitions(reader).ToList();
        }
        catch (IOException)
        {
            return Array.Empty<ModCategoryDefinition>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<ModCategoryDefinition>();
        }
    }

    private static IEnumerable<ModCategoryDefinition> ParseDefinitions(TextReader reader)
    {
        string? line;
        string? currentName = null;
        var keywords = new List<string>();
        var tags = new List<string>();

        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = line.Trim(TrimCharacters);
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(currentName) && (keywords.Count > 0 || tags.Count > 0))
                {
                    yield return new ModCategoryDefinition(currentName!, keywords.ToArray(), tags.ToArray());
                }

                currentName = trimmed.TrimStart('#').Trim();
                keywords.Clear();
                tags.Clear();
                continue;
            }

            if (currentName is null)
            {
                // Ignore matcher lines that appear before the first category declaration.
                continue;
            }

            if (trimmed.StartsWith('%'))
            {
                string tag = trimmed.Substring(1).Trim();
                if (tag.Length > 0)
                {
                    tags.Add(tag);
                }

                continue;
            }

            keywords.Add(trimmed);
        }

        if (!string.IsNullOrWhiteSpace(currentName) && (keywords.Count > 0 || tags.Count > 0))
        {
            yield return new ModCategoryDefinition(currentName!, keywords.ToArray(), tags.ToArray());
        }
    }
}
