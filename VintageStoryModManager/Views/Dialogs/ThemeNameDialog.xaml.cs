using System.Windows;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.Views.Dialogs;

public partial class ThemeNameDialog : Window
{
    public ThemeNameDialog(string? initialName)
    {
        InitializeComponent();
        ThemeName = initialName ?? string.Empty;
        DataContext = this;
    }

    public string ThemeName { get; set; }

    private void OkButton_OnClick(object sender, RoutedEventArgs e)
    {
        var enteredName = ThemeNameTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(enteredName))
        {
            ModManagerMessageBox.Show(
                this,
                "Please enter a theme name.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        ThemeName = enteredName;
        DialogResult = true;
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
