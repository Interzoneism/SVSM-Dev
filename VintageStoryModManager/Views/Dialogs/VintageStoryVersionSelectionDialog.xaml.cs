using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VintageStoryModManager.Views.Dialogs;

public partial class VintageStoryVersionSelectionDialog : Window
{
    private readonly List<string> _versions;

    public VintageStoryVersionSelectionDialog(Window owner, IEnumerable<string> versions)
    {
        InitializeComponent();

        Owner = owner;
        _versions = versions?.Where(version => !string.IsNullOrWhiteSpace(version))
            .Select(version => version.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        VersionsListBox.ItemsSource = _versions;

        if (_versions.Count > 0)
        {
            VersionsListBox.SelectedIndex = 0;
        }

        UpdateSelectButtonState();
    }

    public string? SelectedVersion => VersionsListBox.SelectedItem as string;

    private void SelectButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedVersion is null)
        {
            return;
        }

        DialogResult = true;
    }

    private void VersionsListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedVersion is null)
        {
            return;
        }

        DialogResult = true;
    }

    private void VersionsListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectButtonState();
    }

    private void UpdateSelectButtonState()
    {
        SelectButton.IsEnabled = SelectedVersion is not null;
    }
}
