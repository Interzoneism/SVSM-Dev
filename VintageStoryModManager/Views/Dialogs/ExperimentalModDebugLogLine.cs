using System;

namespace VintageStoryModManager.Views.Dialogs;

public sealed class ExperimentalModDebugLogLine
{
    private const int MaxLogLineLength = 300;

    private static readonly string[] HighlightKeywords =
    {
        "error",
        "warning",
        "exception",
        "failed ",
        "missing"
    };

    private ExperimentalModDebugLogLine(string text, bool isHighlighted)
    {
        Text = text;
        IsHighlighted = isHighlighted;
    }

    public string Text { get; }

    public bool IsHighlighted { get; }

    public static ExperimentalModDebugLogLine FromLogEntry(string rawText)
    {
        ArgumentNullException.ThrowIfNull(rawText);

        string text = rawText.Length > MaxLogLineLength
            ? string.Concat(rawText.AsSpan(0, MaxLogLineLength), "... (log line too long to show)")
            : rawText;

        bool isHighlighted = ContainsHighlightKeyword(rawText);
        return new ExperimentalModDebugLogLine(text, isHighlighted);
    }

    public static ExperimentalModDebugLogLine FromPlainText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new ExperimentalModDebugLogLine(text, false);
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
