using System;

namespace VintageStoryModManager.Views.Dialogs;

public sealed class ExperimentalModDebugLogLine
{
    private const int MaxLogLineLength = 300;

    private static readonly string[] HighlightKeywords =
    {
        "error",
        "warning",
        "fault",
        "missing"
    };

    private ExperimentalModDebugLogLine(string text, bool isHighlighted, string? modName = null)
    {
        Text = text;
        IsHighlighted = isHighlighted;
        ModName = modName;
    }

    public string Text { get; }

    public bool IsHighlighted { get; }

    public string? ModName { get; }

    public static ExperimentalModDebugLogLine FromLogEntry(string rawText, string? modName = null)
    {
        ArgumentNullException.ThrowIfNull(rawText);

        string text = rawText.Length > MaxLogLineLength
            ? string.Concat(rawText.AsSpan(0, MaxLogLineLength), "... (log line too long to show)")
            : rawText;

        bool isHighlighted = ContainsHighlightKeyword(rawText);
        return new ExperimentalModDebugLogLine(text, isHighlighted, modName);
    }

    public static ExperimentalModDebugLogLine FromPlainText(string text, string? modName = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new ExperimentalModDebugLogLine(text, false, modName);
    }

    private static bool ContainsHighlightKeyword(string text)
    {
        foreach (string keyword in HighlightKeywords)
        {
            if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
