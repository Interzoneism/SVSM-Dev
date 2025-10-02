using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VintageStoryModManager.Services;

internal static class VersionStringUtility
{
    public static string? Normalize(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (char c in version)
        {
            if (char.IsDigit(c) || c == '.')
            {
                builder.Append(c);
            }
            else if (builder.Length > 0)
            {
                break;
            }
        }

        if (builder.Length == 0)
        {
            return null;
        }

        string trimmed = builder.ToString().Trim('.');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        string[] parts = trimmed
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        IEnumerable<string> normalized = parts.Length > 3 ? parts.Take(3) : parts;
        string candidate = string.Join('.', normalized);
        return string.IsNullOrWhiteSpace(candidate) ? null : candidate;
    }
}
