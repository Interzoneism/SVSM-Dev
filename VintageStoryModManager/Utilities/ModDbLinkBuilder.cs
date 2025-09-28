using System;
using System.Text;

namespace VintageStoryModManager.Utilities;

/// <summary>
/// Provides helpers for constructing links to the official Vintage Story Mod DB.
/// </summary>
internal static class ModDbLinkBuilder
{
    private const string BaseUrl = "https://mods.vintagestory.at/";

    public static Uri? TryCreateEntryUri(string? modId)
    {
        string slug = CreateSlug(modId);
        if (string.IsNullOrEmpty(slug))
        {
            return null;
        }

        if (!Uri.TryCreate(BaseUrl + slug, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        return uri;
    }

    private static string CreateSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            if (char.IsLetter(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (char.IsDigit(ch))
            {
                builder.Append(ch);
            }
            else if (ch == '-' || ch == '_')
            {
                if (builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }
            }
            else if (char.IsWhiteSpace(ch))
            {
                if (builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }
            }
        }

        string slug = builder.ToString().Trim('-');
        if (slug.Length > 0)
        {
            return slug;
        }

        builder.Clear();
        foreach (char ch in value)
        {
            if (char.IsLetter(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (char.IsDigit(ch))
            {
                if (builder.Length == 0)
                {
                    builder.Append('m');
                }

                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
