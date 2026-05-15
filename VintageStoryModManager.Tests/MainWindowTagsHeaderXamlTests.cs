using System.Xml.Linq;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class MainWindowTagsHeaderXamlTests
{
    [Fact]
    public void InstalledTagsHeaderPopupIncludesClearOption()
    {
        var xamlPath = Path.Combine(
            FindRepositoryRoot(),
            "VintageStoryModManager",
            "Views",
            "MainWindow.xaml");

        var document = XDocument.Load(Path.GetFullPath(xamlPath));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var clearButton = document
            .Descendants(presentation + "Popup")
            .Where(popup => popup.Descendants(presentation + "ItemsControl").Any(itemsControl =>
                itemsControl.Attribute("ItemsSource")?.Value.Contains("InstalledTagFilters", StringComparison.Ordinal) == true))
            .SelectMany(popup => popup.Descendants(presentation + "Button"))
            .SingleOrDefault(button => string.Equals(
                button.Attribute("Content")?.Value,
                "Clear",
                StringComparison.Ordinal));

        Assert.NotNull(clearButton);
        Assert.Contains("ClearInstalledTagFiltersCommand", clearButton.Attribute("Command")?.Value);
    }

    private static string FindRepositoryRoot()
    {
        var candidates = new[]
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        };

        foreach (var candidate in candidates)
        {
            var directory = new DirectoryInfo(candidate);
            while (directory is not null)
            {
                var solutionPath = Path.Combine(directory.FullName, "ImprovedModMenu.sln");
                if (File.Exists(solutionPath)) return directory.FullName;

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ImprovedModMenu.sln.");
    }
}
