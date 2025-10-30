using System;
using System.Collections.Generic;
using System.Windows;

namespace VintageStoryModManager.Views.Dialogs;

public partial class ExperimentalModDebugDialog : Window
{
    public ExperimentalModDebugDialog(string modId, IReadOnlyList<string> logLines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        ArgumentNullException.ThrowIfNull(logLines);

        InitializeComponent();

        ModId = modId;
        LogLines = logLines;
        HeaderText = $"Log entries mentioning '{modId}'";
        EmptyMessage = $"No log entries referencing '{modId}' were found.";

        DataContext = this;
    }

    public string ModId { get; }

    public IReadOnlyList<string> LogLines { get; }

    public string HeaderText { get; }

    public string EmptyMessage { get; }

    public bool HasLogLines => LogLines.Count > 0;

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
