using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

/// <summary>
/// Interaction logic for ModBrowserWindow.xaml
/// </summary>
public partial class ModBrowserWindow : Window
{
    private readonly IModApiService _modApiService;
    private readonly UserConfigurationService _userConfigService;

    public ModBrowserWindow()
    {
        InitializeComponent();

        // Set up dependency injection manually for simplicity
        // In a larger application, you would use a DI container
        var httpClient = new HttpClient();
        _modApiService = new ModApiService(httpClient);
        _userConfigService = new UserConfigurationService();
        _userConfigService.EnablePersistence();
        var viewModel = new ModBrowserViewModel(_modApiService, _userConfigService);

        viewModel.SetInstallModCallback(InstallModAsync);

        ModBrowser.DataContext = viewModel;
    }

    private async Task InstallModAsync(DownloadableMod mod)
    {
        if (mod.Releases is null || mod.Releases.Count == 0)
        {
            WpfMessageBox.Show("No downloadable releases are available for this mod.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var release = mod.Releases
            .OrderByDescending(r => DateTime.TryParse(r.Created, out var date) ? date : DateTime.MinValue)
            .FirstOrDefault();

        if (release is null)
        {
            WpfMessageBox.Show("No compatible releases are available for this mod.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(_userConfigService.DataDirectory))
        {
            WpfMessageBox.Show(
                "The VintagestoryData folder is not available. Please set it before installing mods.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        var modsDirectory = Path.Combine(_userConfigService.DataDirectory, "Mods");

        try
        {
            Directory.CreateDirectory(modsDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                       or NotSupportedException)
        {
            WpfMessageBox.Show($"The Mods folder could not be accessed:{Environment.NewLine}{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        var modIdStr = mod.ModIdStr ?? mod.ModId.ToString();
        var defaultName = string.IsNullOrWhiteSpace(modIdStr) ? "mod" : modIdStr;
        var versionPart = string.IsNullOrWhiteSpace(release.ModVersion) ? "latest" : release.ModVersion;
        var fallbackFileName = $"{defaultName}-{versionPart}.zip";

        var releaseFileName = release.Filename;
        if (string.IsNullOrWhiteSpace(releaseFileName))
        {
            releaseFileName = fallbackFileName;
        }
        else
        {
            releaseFileName = Path.GetFileName(releaseFileName);
        }

        var sanitizedFileName = SanitizeFileName(releaseFileName, fallbackFileName);
        if (string.IsNullOrWhiteSpace(Path.GetExtension(sanitizedFileName))) sanitizedFileName += ".zip";

        var destinationPath = EnsureUniqueFilePath(Path.Combine(modsDirectory, sanitizedFileName));
        var downloadUrl = new Uri($"https://mods.vintagestory.at{release.MainFile}");

        var progress = new Progress<double>(p => Title = $"Downloading {mod.Name}... {p:0}%");
        var success = await _modApiService.DownloadModAsync(downloadUrl.ToString(), destinationPath, progress);

        Title = "Mod Browser";

        if (!success)
        {
            WpfMessageBox.Show("The installation failed.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        WpfMessageBox.Show($"{mod.Name} has been installed successfully.",
            "Simple VS Manager",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static string SanitizeFileName(string? fileName, string fallback)
    {
        var name = string.IsNullOrWhiteSpace(fileName) ? fallback : fileName;
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);

        foreach (var c in name) builder.Append(Array.IndexOf(invalidChars, c) >= 0 ? '_' : c);

        var sanitized = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }

    private static string EnsureUniqueFilePath(string path)
    {
        if (!File.Exists(path)) return path;

        var directory = Path.GetDirectoryName(path);
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        if (string.IsNullOrWhiteSpace(directory)) directory = Directory.GetCurrentDirectory();

        var counter = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{fileName} ({counter}){extension}");
            counter++;
        } while (File.Exists(candidate));

        return candidate;
    }
}