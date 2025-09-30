using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel? _viewModel;
    private bool _isInitialized;

    public MainWindow()
    {
        InitializeComponent();

        try
        {
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize the mod manager:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Application.Current.Shutdown();
        }
    }

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        if (_isInitialized || _viewModel == null)
        {
            return;
        }

        _isInitialized = true;
        await InitializeAsync(_viewModel);
    }

    private static async Task InitializeAsync(MainViewModel viewModel)
    {
        try
        {
            await viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load mods:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ModsDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid dataGrid)
        {
            dataGrid.SelectedIndex = -1;
            dataGrid.UnselectAll();
            dataGrid.UnselectAllCells();
        }
    }

    private void ModDatabasePageButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ModListItemViewModel mod }
            && mod.OpenModDatabasePageCommand is ICommand command
            && command.CanExecute(null))
        {
            command.Execute(null);
        }

        e.Handled = true;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {

    }
}
