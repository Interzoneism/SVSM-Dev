using System.Xml.Linq;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ContextMenuXamlTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData(
        "ModsDataGrid",
        "ModsDataGrid_OnPreviewMouseRightButtonDown",
        "InstalledModsContextMenu",
        "InstalledModsContextMenu_OnOpened")]
    [InlineData(
        "LocalModlistsDataGrid",
        "LocalModlistsDataGrid_OnPreviewMouseRightButtonDown",
        "LocalModlistsContextMenu",
        "LocalModlistsContextMenu_OnOpened")]
    [InlineData(
        "CloudModlistsDataGrid",
        "CloudModlistsDataGrid_OnPreviewMouseRightButtonDown",
        "CloudModlistsContextMenu",
        "CloudModlistsContextMenu_OnOpened")]
    public void DataGridsExposeDynamicAutoSizingContextMenus(
        string dataGridName,
        string previewHandler,
        string contextMenuName,
        string openedHandler)
    {
        var document = LoadMainWindow();
        var dataGrid = document
            .Descendants(Presentation + "DataGrid")
            .Single(element => string.Equals(
                element.Attribute(Xaml + "Name")?.Value,
                dataGridName,
                StringComparison.Ordinal));
        var contextMenu = dataGrid
            .Elements(Presentation + "DataGrid.ContextMenu")
            .Elements(Presentation + "ContextMenu")
            .Single();

        Assert.Equal(previewHandler, dataGrid.Attribute("PreviewMouseRightButtonDown")?.Value);
        Assert.Equal(contextMenuName, contextMenu.Attribute(Xaml + "Name")?.Value);
        Assert.Equal(openedHandler, contextMenu.Attribute("Opened")?.Value);
        Assert.Equal("240", contextMenu.Attribute("MinWidth")?.Value);
        Assert.Null(contextMenu.Attribute("Width"));
        Assert.Null(contextMenu.Attribute("MaxWidth"));
    }

    private static XDocument LoadMainWindow()
    {
        var xamlPath = Path.Combine(
            FindRepositoryRoot(),
            "VintageStoryModManager",
            "Views",
            "MainWindow.xaml");

        return XDocument.Load(Path.GetFullPath(xamlPath));
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
                if (File.Exists(Path.Combine(directory.FullName, "ImprovedModMenu.sln")))
                    return directory.FullName;

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ImprovedModMenu.sln.");
    }
}
