using System.Windows;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Views.Dialogs;

public partial class ColorPickerDialog : Window
{
    private readonly ColorPickerViewModel _viewModel;

    public ColorPickerDialog(string? initialHex)
    {
        InitializeComponent();
        _viewModel = new ColorPickerViewModel(initialHex);
        DataContext = _viewModel;
    }

    public string SelectedHexValue => _viewModel.NormalizedHexValue;

    private void SelectButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.HasValidColor)
        {
            return;
        }

        DialogResult = true;
    }
}
