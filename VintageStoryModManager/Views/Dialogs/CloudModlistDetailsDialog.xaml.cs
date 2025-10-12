using System;
using System.Windows;
using System.Windows.Controls;

namespace VintageStoryModManager.Views.Dialogs;

public partial class CloudModlistDetailsDialog : Window
{
    public CloudModlistDetailsDialog(Window owner, string? suggestedName)
    {
        InitializeComponent();

        Owner = owner;
        NameTextBox.Text = string.IsNullOrWhiteSpace(suggestedName)
            ? string.Empty
            : suggestedName;
        NameTextBox.SelectAll();
        UpdateConfirmButtonState();
    }

    public string ModlistName => NameTextBox.Text.Trim();

    public string? ModlistDescription => string.IsNullOrWhiteSpace(DescriptionTextBox.Text)
        ? null
        : DescriptionTextBox.Text.Trim();

    public string? ModlistVersion => string.IsNullOrWhiteSpace(VersionTextBox.Text)
        ? null
        : VersionTextBox.Text.Trim();

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            return;
        }

        DialogResult = true;
    }

    private void NameTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateConfirmButtonState();
    }

    private void UpdateConfirmButtonState()
    {
        ConfirmButton.IsEnabled = !string.IsNullOrWhiteSpace(NameTextBox.Text);
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        NameTextBox.Focus();
        NameTextBox.SelectAll();
    }
}
