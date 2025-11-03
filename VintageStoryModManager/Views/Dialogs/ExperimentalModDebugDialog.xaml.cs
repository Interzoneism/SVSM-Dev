using System;
using System.Collections.Generic;
using System.Windows;

namespace VintageStoryModManager.Views.Dialogs;

public partial class ExperimentalModDebugDialog : Window
{
    public ExperimentalModDebugDialog(string modId, IReadOnlyList<ExperimentalModDebugLogLine> logLines)
        : this(
            $"Log entries mentioning '{modId}'",
            $"No log entries referencing '{modId}' were found.",
            logLines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        ModId = modId;
    }

    public ExperimentalModDebugDialog(
        string headerText,
        string emptyMessage,
        IReadOnlyList<ExperimentalModDebugLogLine> logLines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerText);
        ArgumentException.ThrowIfNullOrWhiteSpace(emptyMessage);
        ArgumentNullException.ThrowIfNull(logLines);

        InitializeComponent();

        HeaderText = headerText;
        EmptyMessage = emptyMessage;
        LogLines = logLines;

        DataContext = this;
    }

    public string? ModId { get; }

    public IReadOnlyList<ExperimentalModDebugLogLine> LogLines { get; }

    public string HeaderText { get; }

    public string EmptyMessage { get; }

    public bool HasLogLines => LogLines.Count > 0;

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
