using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using WinForms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = System.Windows.MessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow : Window
{
    private readonly UserConfigurationService _userConfiguration;
    private MainViewModel? _viewModel;
    private string? _dataDirectory;
    private string? _gameDirectory;
    private bool _isInitializing;

    public MainWindow()
    {
        InitializeComponent();

        _userConfiguration = new UserConfigurationService();

        if (!TryInitializePaths())
        {
            WpfApplication.Current?.Shutdown();
            return;
        }

        try
        {
            InitializeViewModel();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Failed to initialize the mod manager:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            WpfApplication.Current?.Shutdown();
            return;
        }

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;

        if (_viewModel != null)
        {
            await InitializeViewModelAsync(_viewModel);
        }
    }

    private void InitializeViewModel()
    {
        if (string.IsNullOrWhiteSpace(_dataDirectory))
        {
            throw new InvalidOperationException("The data directory is not set.");
        }

        _viewModel = new MainViewModel(_dataDirectory, _userConfiguration);
        DataContext = _viewModel;
    }

    private async Task InitializeViewModelAsync(MainViewModel viewModel)
    {
        if (_isInitializing)
        {
            return;
        }

        _isInitializing = true;
        try
        {
            await viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Failed to load mods:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private bool TryInitializePaths()
    {
        if (!TryValidateDataDirectory(_userConfiguration.DataDirectory, out _dataDirectory, out _))
        {
            TryValidateDataDirectory(DataDirectoryLocator.Resolve(), out _dataDirectory, out _);
        }

        if (_dataDirectory is null)
        {
            WpfMessageBox.Show("The Vintage Story data folder could not be located. Please select it to continue.",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            _dataDirectory = PromptForDirectory(
                "Select your VintagestoryData folder",
                _userConfiguration.DataDirectory ?? DataDirectoryLocator.Resolve(),
                TryValidateDataDirectory,
                allowCancel: false);

            if (_dataDirectory is null)
            {
                return false;
            }
        }

        if (!TryValidateGameDirectory(_userConfiguration.GameDirectory, out _gameDirectory, out _))
        {
            TryValidateGameDirectory(GameDirectoryLocator.Resolve(), out _gameDirectory, out _);
        }

        if (_gameDirectory is null)
        {
            WpfMessageBox.Show("The Vintage Story installation folder could not be located. Please select it to continue.",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            _gameDirectory = PromptForDirectory(
                "Select your Vintage Story installation folder",
                _userConfiguration.GameDirectory ?? GameDirectoryLocator.Resolve(),
                TryValidateGameDirectory,
                allowCancel: false);

            if (_gameDirectory is null)
            {
                return false;
            }
        }

        _userConfiguration.SetDataDirectory(_dataDirectory);
        _userConfiguration.SetGameDirectory(_gameDirectory);
        return true;
    }

    private async Task ReloadViewModelAsync()
    {
        if (string.IsNullOrWhiteSpace(_dataDirectory))
        {
            return;
        }

        try
        {
            var viewModel = new MainViewModel(_dataDirectory, _userConfiguration);
            _viewModel = viewModel;
            DataContext = viewModel;
            await InitializeViewModelAsync(viewModel);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Failed to reload mods:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private delegate bool PathValidator(string? path, out string? normalizedPath, out string? errorMessage);

    private string? PromptForDirectory(string description, string? initialPath, PathValidator validator, bool allowCancel)
    {
        string? candidate = initialPath;

        while (true)
        {
            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = description,
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };

            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
            {
                dialog.SelectedPath = candidate;
            }

            WinForms.DialogResult result = dialog.ShowDialog();
            if (result != WinForms.DialogResult.OK)
            {
                if (allowCancel)
                {
                    return null;
                }

                MessageBoxResult exit = WpfMessageBox.Show(
                    "You must select a folder to continue. Do you want to exit the application?",
                    "Vintage Story Mod Manager",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (exit == MessageBoxResult.Yes)
                {
                    return null;
                }

                continue;
            }

            candidate = dialog.SelectedPath;
            if (validator(candidate, out string? normalized, out string? errorMessage))
            {
                return normalized;
            }

            WpfMessageBox.Show(errorMessage ?? "The selected folder is not valid.",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static bool TryValidateDataDirectory(string? path, out string? normalizedPath, out string? errorMessage)
    {
        normalizedPath = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            errorMessage = "No folder was selected.";
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            errorMessage = "The folder path is invalid.";
            return false;
        }

        if (!Directory.Exists(normalizedPath))
        {
            errorMessage = "The folder does not exist.";
            return false;
        }

        bool hasClientSettings = File.Exists(Path.Combine(normalizedPath, "clientsettings.json"));
        bool hasMods = Directory.Exists(Path.Combine(normalizedPath, "Mods"));
        bool hasConfig = Directory.Exists(Path.Combine(normalizedPath, "ModConfig"));

        if (!hasClientSettings && !hasMods && !hasConfig)
        {
            errorMessage = "The folder does not appear to be a VintagestoryData directory.";
            return false;
        }

        return true;
    }

    private static bool TryValidateGameDirectory(string? path, out string? normalizedPath, out string? errorMessage)
    {
        normalizedPath = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            errorMessage = "No folder was selected.";
            return false;
        }

        string candidate;
        try
        {
            candidate = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            errorMessage = "The folder path is invalid.";
            return false;
        }

        if (File.Exists(candidate))
        {
            string? directory = Path.GetDirectoryName(candidate);
            if (string.IsNullOrWhiteSpace(directory))
            {
                errorMessage = "The folder path is invalid.";
                return false;
            }

            candidate = directory;
        }

        if (!Directory.Exists(candidate))
        {
            errorMessage = "The folder does not exist.";
            return false;
        }

        string? executable = GameDirectoryLocator.FindExecutable(candidate);
        if (executable is null)
        {
            errorMessage = "The folder does not contain a Vintage Story executable.";
            return false;
        }

        normalizedPath = candidate;
        return true;
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
        if (sender is WpfButton { DataContext: ModListItemViewModel mod }
            && mod.OpenModDatabasePageCommand is ICommand command
            && command.CanExecute(null))
        {
            command.Execute(null);
        }

        e.Handled = true;
    }

    private async void RefreshModsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.RefreshCommand == null)
        {
            return;
        }

        try
        {
            await _viewModel.RefreshCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Failed to refresh mods:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void SelectDataFolderMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        string? selected = PromptForDirectory(
            "Select your VintagestoryData folder",
            _dataDirectory,
            TryValidateDataDirectory,
            allowCancel: true);

        if (selected is null)
        {
            return;
        }

        if (string.Equals(selected, _dataDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _dataDirectory = selected;
        _userConfiguration.SetDataDirectory(selected);
        await ReloadViewModelAsync();
    }

    private void SelectGameFolderMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        string? selected = PromptForDirectory(
            "Select your Vintage Story installation folder",
            _gameDirectory,
            TryValidateGameDirectory,
            allowCancel: true);

        if (selected is null)
        {
            return;
        }

        if (string.Equals(selected, _gameDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _gameDirectory = selected;
        _userConfiguration.SetGameDirectory(selected);
    }

    private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LaunchGameButton_OnClick(object sender, RoutedEventArgs e)
    {
        string? executable = GameDirectoryLocator.FindExecutable(_gameDirectory);
        if (executable is null)
        {
            WpfMessageBox.Show("The Vintage Story executable could not be found. Verify the game folder in File > Set Game Folder.",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable)!,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Failed to launch Vintage Story:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenModFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFolder(_dataDirectory is null ? null : Path.Combine(_dataDirectory, "Mods"), "mods");
    }

    private void OpenConfigFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFolder(_dataDirectory is null ? null : Path.Combine(_dataDirectory, "ModConfig"), "config");
    }

    private void OpenLogsFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFolder(_dataDirectory is null ? null : Path.Combine(_dataDirectory, "Logs"), "logs");
    }

    private static void OpenFolder(string? path, string description)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            WpfMessageBox.Show($"The {description} folder is not available. Please verify the VintagestoryData folder from File > Set Data Folder.",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!Directory.Exists(path))
        {
            WpfMessageBox.Show($"The {description} folder could not be found at:\n{path}\nPlease verify the VintagestoryData folder from File > Set Data Folder.",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Failed to open the {description} folder:\n{ex.Message}",
                "Vintage Story Mod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SavePresetButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        string defaultName = _viewModel.SelectedPreset ?? string.Empty;

        var dialog = new PresetNameDialog
        {
            Owner = this
        };
        dialog.SetInitialName(defaultName);

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        string presetName = dialog.PresetName;
        if (string.IsNullOrWhiteSpace(presetName))
        {
            return;
        }

        if (_viewModel.ContainsPreset(presetName))
        {
            MessageBoxResult confirm = WpfMessageBox.Show(
                $"A preset named \"{presetName}\" already exists. Do you want to replace it?",
                "Save Preset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }
        }

        try
        {
            if (!_viewModel.TrySavePreset(presetName, out string? errorMessage))
            {
                if (string.IsNullOrWhiteSpace(errorMessage))
                {
                    errorMessage = "Failed to save the preset.";
                }

                WpfMessageBox.Show(
                    errorMessage!,
                    "Save Preset",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"Failed to save preset:\n{ex.Message}",
                "Save Preset",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
