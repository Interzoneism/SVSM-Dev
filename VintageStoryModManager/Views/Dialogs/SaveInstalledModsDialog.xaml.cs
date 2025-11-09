using System.Windows;
using System.Windows.Controls;

namespace VintageStoryModManager.Views.Dialogs;

public partial class SaveInstalledModsDialog : Window
{
    public SaveInstalledModsDialog(string? defaultListName = null)
    {
        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(defaultListName))
        {
            NameTextBox.Text = defaultListName.Trim();
        }

        UpdateConfirmButtonState();
    }

    public string ListName => NameTextBox.Text.Trim();

    public string? Description => NormalizeOptionalText(DescriptionTextBox.Text);

    public string? ConfigDescription => NormalizeOptionalText(ConfigDescriptionTextBox.Text);

    private static string? NormalizeOptionalText(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateConfirmButtonState();
        NameTextBox.Focus();
        NameTextBox.SelectAll();
    }

    private void NameTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateConfirmButtonState();
    }

    private void UpdateConfirmButtonState()
    {
        if (ConfirmButton is null)
        {
            return;
        }

        ConfirmButton.IsEnabled = !string.IsNullOrWhiteSpace(NameTextBox.Text);
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            return;
        }

        DialogResult = true;
        Close();
    }
}
