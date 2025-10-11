using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VintageStoryModManager.Views.Dialogs;

public partial class SelectConfigFileDialog : Window
{
    public sealed record ConfigFileOption(string FilePath, string DisplayName);

    public ObservableCollection<ConfigFileOption> ConfigFiles { get; }

    public ConfigFileOption? SelectedConfigFile { get; set; }

    public string? SelectedFilePath { get; private set; }

    public bool ShouldChooseFiles { get; private set; }

    public SelectConfigFileDialog(IEnumerable<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        ConfigFiles = new ObservableCollection<ConfigFileOption>(
            filePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => new ConfigFileOption(path, BuildDisplayName(path))));

        if (ConfigFiles.Count > 0)
        {
            SelectedConfigFile = ConfigFiles[0];
        }

        InitializeComponent();
        DataContext = this;

        Loaded += (_, _) => UpdateEditButtonState();
    }

    private static string BuildDisplayName(string path)
    {
        string fileName = Path.GetFileName(path);
        string withoutExtension = Path.GetFileNameWithoutExtension(fileName);
        if (!string.IsNullOrWhiteSpace(withoutExtension))
        {
            return withoutExtension;
        }

        return string.IsNullOrWhiteSpace(fileName) ? path : fileName;
    }

    private void UpdateEditButtonState()
    {
        if (EditButton != null)
        {
            EditButton.IsEnabled = SelectedConfigFile is not null;
        }
    }

    private void ConfigFilesListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateEditButtonState();
    }

    private void ConfigFilesListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedConfigFile is null)
        {
            return;
        }

        SelectedFilePath = SelectedConfigFile.FilePath;
        DialogResult = true;
    }

    private void EditButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedConfigFile is null)
        {
            return;
        }

        SelectedFilePath = SelectedConfigFile.FilePath;
        DialogResult = true;
    }

    private void ChooseFilesButton_OnClick(object sender, RoutedEventArgs e)
    {
        ShouldChooseFiles = true;
        DialogResult = false;
    }
}
