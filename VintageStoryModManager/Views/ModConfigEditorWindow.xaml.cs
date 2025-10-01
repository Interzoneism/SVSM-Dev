using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using VintageStoryModManager.ViewModels;
using WpfMessageBox = System.Windows.MessageBox;

namespace VintageStoryModManager.Views;

public partial class ModConfigEditorWindow : Window
{
    public ModConfigEditorWindow(ModConfigEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ModConfigEditorViewModel viewModel)
        {
            return;
        }

        try
        {
            viewModel.Save();
            DialogResult = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or JsonException)
        {
            WpfMessageBox.Show(this,
                $"Failed to save the configuration:\n{ex.Message}",
                "Edit Config",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
