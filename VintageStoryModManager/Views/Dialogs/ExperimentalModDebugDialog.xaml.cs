using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace VintageStoryModManager.Views.Dialogs;

public partial class ExperimentalModDebugDialog : Window, INotifyPropertyChanged
{
    private bool _showOnlyHighlighted;
    private ICollectionView? _filteredLogLines;

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

        // Initialize ModId to null to prevent crashes when accessing it
        ModId = null;

        InitializeComponent();

        HeaderText = headerText;
        EmptyMessage = emptyMessage;
        LogLines = logLines;

        // Initialize the filtered view
        _filteredLogLines = CollectionViewSource.GetDefaultView(LogLines);
        _filteredLogLines.Filter = FilterLogLine;

        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? ModId { get; private set; }

    public IReadOnlyList<ExperimentalModDebugLogLine> LogLines { get; }

    public ICollectionView FilteredLogLines => _filteredLogLines ?? CollectionViewSource.GetDefaultView(LogLines);

    public string HeaderText { get; }

    public string EmptyMessage { get; }

    public bool HasLogLines => LogLines.Count > 0;

    public bool ShowOnlyHighlighted
    {
        get => _showOnlyHighlighted;
        set
        {
            if (_showOnlyHighlighted != value)
            {
                _showOnlyHighlighted = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowOnlyHighlighted)));
                _filteredLogLines?.Refresh();
            }
        }
    }

    private bool FilterLogLine(object obj)
    {
        if (obj is not ExperimentalModDebugLogLine logLine)
        {
            return true;
        }

        if (!ShowOnlyHighlighted)
        {
            return true;
        }

        return logLine.IsHighlighted;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
