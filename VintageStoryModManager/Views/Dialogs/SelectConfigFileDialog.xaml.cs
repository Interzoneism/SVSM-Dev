using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace VintageStoryModManager.Views.Dialogs;

public partial class SelectConfigFileDialog : Window
{
    public SelectConfigFileDialog(string modDisplayName, IReadOnlyList<string>? configPaths)
    {
        InitializeComponent();

        Title = string.IsNullOrWhiteSpace(modDisplayName)
            ? "Select Config File"
            : $"Select Config File - {modDisplayName}";

        ConfigPaths = new ObservableCollection<string>(configPaths ?? Array.Empty<string>());
        if (ConfigPaths.Count > 0)
        {
            SelectedPath = ConfigPaths[0];
        }

        DataContext = this;
    }

    public ObservableCollection<string> ConfigPaths { get; }

    public string? SelectedPath { get; set; }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SelectedPath)
            && ConfigListBox.SelectedItem is string selected
            && !string.IsNullOrWhiteSpace(selected))
        {
            SelectedPath = selected;
        }

        if (!string.IsNullOrWhiteSpace(SelectedPath))
        {
            DialogResult = true;
        }
    }

    private void OnConfigListBoxDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ConfigListBox.SelectedItem is string selected
            && !string.IsNullOrWhiteSpace(selected))
        {
            SelectedPath = selected;
            DialogResult = true;
        }
    }
}
