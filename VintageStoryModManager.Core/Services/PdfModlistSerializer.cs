using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VintageStoryModManager.Core.Models;

namespace VintageStoryModManager.Core.Services;

/// <summary>
/// Provides helpers for encoding modlist presets into PDF-friendly payloads and
/// recovering them when loading from a PDF export.
/// </summary>
public static class PdfModlistSerializer
{
    private static readonly JsonSerializerOptions SerializationOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions DeserializationOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string SerializeToBase64(SerializablePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        string serialized = JsonSerializer.Serialize(preset, SerializationOptions);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(serialized));
    }

    public static bool TryDeserializeFromJson(string json, out SerializablePreset? preset, out string? errorMessage)
    {
        preset = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            errorMessage = "The selected file was empty.";
            return false;
        }

        try
        {
            preset = JsonSerializer.Deserialize<SerializablePreset>(json, DeserializationOptions);
        }
        catch (JsonException ex)
        {
            errorMessage = ex.Message;
            return false;
        }

        if (preset is null)
        {
            errorMessage = "The selected file was empty.";
            return false;
        }

        return true;
    }

    public static bool TryExtractModlistJson(string pdfText, out string? json, out string? errorMessage)
    {
        json = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(pdfText))
        {
            errorMessage = "The PDF did not contain any modlist data.";
            return false;
        }

        int startIndex = pdfText.IndexOf("###", StringComparison.Ordinal);
        if (startIndex < 0)
        {
            errorMessage = "The PDF did not contain a modlist section.";
            return false;
        }

        startIndex += 3;
        int endIndex = pdfText.IndexOf("###", startIndex, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            errorMessage = "The PDF did not contain the end of the modlist section.";
            return false;
        }

        if (endIndex <= startIndex)
        {
            errorMessage = "The PDF did not contain any modlist data.";
            return false;
        }

        string rawContent = pdfText.Substring(startIndex, endIndex - startIndex);
        string normalized = NormalizePayload(rawContent);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            errorMessage = "The PDF did not contain any modlist data.";
            return false;
        }

        if (IsProbableJson(normalized))
        {
            json = normalized;
            return true;
        }

        if (TryDecodeBase64(normalized, out string? decoded))
        {
            json = decoded;
            return true;
        }

        errorMessage = "The PDF modlist data was not in a recognized format.";
        return false;
    }

    public static string NormalizePayload(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        string sanitized = content
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace("\0", string.Empty)
            .Replace("\u00A0", " ");

        var builder = new StringBuilder();
        string[] lines = sanitized.Split('\n');

        foreach (string line in lines)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(line.TrimEnd());
        }

        return builder.ToString().Trim();
    }

    private static bool IsProbableJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        foreach (char ch in content)
        {
            if (char.IsWhiteSpace(ch))
            {
                continue;
            }

            return ch == '{' || ch == '[';
        }

        return false;
    }

    private static bool TryDecodeBase64(string content, out string? decoded)
    {
        decoded = null;

        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        string compact = RemoveWhitespace(content);
        if (compact.Length == 0 || compact.Length % 4 != 0)
        {
            return false;
        }

        Span<byte> buffer = new byte[compact.Length];
        if (!Convert.TryFromBase64String(compact, buffer, out int bytesWritten))
        {
            return false;
        }

        decoded = Encoding.UTF8.GetString(buffer[..bytesWritten]);
        return !string.IsNullOrWhiteSpace(decoded);
    }

    private static string RemoveWhitespace(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(content.Length);
        foreach (char ch in content)
        {
            if (!char.IsWhiteSpace(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
