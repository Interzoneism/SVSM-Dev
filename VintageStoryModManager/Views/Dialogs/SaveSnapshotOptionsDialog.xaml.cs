using System;
using System.Globalization;
using System.Windows;

namespace VintageStoryModManager.Views.Dialogs;

public partial class SaveSnapshotOptionsDialog : Window
{
    public SaveSnapshotOptionsDialog(string snapshotType, string snapshotName, bool includeConfigs)
    {
        InitializeComponent();

        string typeDisplay = string.IsNullOrWhiteSpace(snapshotType)
            ? "snapshot"
            : snapshotType;
        string nameDisplay = string.IsNullOrWhiteSpace(snapshotName)
            ? typeDisplay
            : snapshotName;

        Title = string.Create(CultureInfo.CurrentCulture, $"Save {typeDisplay}");
        HeaderTextBlock.Text = string.Create(
            CultureInfo.CurrentCulture,
            $"Choose additional options before saving \"{nameDisplay}\".");
        IncludeConfigsCheckBox.IsChecked = includeConfigs;
    }

    public bool IncludeConfigs => IncludeConfigsCheckBox.IsChecked == true;

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
