using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace VintageStoryModManager.Views.Dialogs;

public partial class SelectConfigFileDialog : Window
{
    public sealed record ConfigFileOption(string FullPath, string FileName, string Directory);

    private readonly List<ConfigFileOption> _options;

    public SelectConfigFileDialog(string modDisplayName, IEnumerable<string> paths)
    {
        InitializeComponent();

        string displayName = string.IsNullOrWhiteSpace(modDisplayName) ? "Unknown Mod" : modDisplayName;
        Title = $"Edit Config - {displayName}";

        _options = paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(CreateOption)
            .ToList();

        Options = _options;
        DataContext = this;

        MessageTextBlock.Text = _options.Count <= 1
            ? $"Select the configuration file to edit for {displayName}."
            : $"Select which configuration file to edit for {displayName}.";

        if (_options.Count > 0)
        {
            ConfigFilesListBox.SelectedIndex = 0;
            ConfigFilesListBox.Focus();
        }
    }

    public IReadOnlyList<ConfigFileOption> Options { get; }

    public string? SelectedPath => (ConfigFilesListBox.SelectedItem as ConfigFileOption)?.FullPath;

    public int SelectedIndex => ConfigFilesListBox.SelectedIndex;

    public bool BrowseRequested { get; private set; }

    private static ConfigFileOption CreateOption(string path)
    {
        string fullPath = path;
        string? fileName = Path.GetFileName(fullPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = fullPath;
        }

        string? directory = Path.GetDirectoryName(fullPath);
        return new ConfigFileOption(fullPath, fileName!, directory ?? string.Empty);
    }

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (ConfigFilesListBox.SelectedItem is null)
        {
            return;
        }

        DialogResult = true;
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        BrowseRequested = true;
        Close();
    }

    private void ConfigFilesListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ConfigFilesListBox.SelectedItem is ConfigFileOption)
        {
            DialogResult = true;
        }
    }
}
