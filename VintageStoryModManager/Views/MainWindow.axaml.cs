using Avalonia.Controls;
using System.Linq;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnModsSorting(object? sender, DataGridColumnEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (e.Column.Tag is not SortKey key)
        {
            return;
        }

        e.Handled = true;

        bool isSameColumn = viewModel.SelectedSortOption?.Key == key;
        bool descending = isSameColumn ? !viewModel.SortDescending : false;

        viewModel.SortDescending = descending;

        var matchingOption = viewModel.SortOptions.FirstOrDefault(option => option.Key == key);
        if (matchingOption != null)
        {
            viewModel.SelectedSortOption = matchingOption;
        }
    }
}