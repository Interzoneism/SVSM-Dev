using System.Xml.Linq;
using System.Runtime.CompilerServices;
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
        "ModsDataGrid_OnContextMenuOpening")]
    [InlineData(
        "LocalModlistsDataGrid",
        "LocalModlistsDataGrid_OnPreviewMouseRightButtonDown",
        "LocalModlistsContextMenu",
        "LocalModlistsDataGrid_OnContextMenuOpening")]
    [InlineData(
        "CloudModlistsDataGrid",
        "CloudModlistsDataGrid_OnPreviewMouseRightButtonDown",
        "CloudModlistsContextMenu",
        "CloudModlistsDataGrid_OnContextMenuOpening")]
    public void DataGridsExposeDynamicAutoSizingContextMenus(
        string dataGridName,
        string previewHandler,
        string contextMenuName,
        string openingHandler)
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
        var uniformContextMenuStyle = document
            .Descendants(Presentation + "Style")
            .Single(element => string.Equals(
                element.Attribute(Xaml + "Key")?.Value,
                "IMM.UniformContextMenuStyle",
                StringComparison.Ordinal));
        var paddingSetter = uniformContextMenuStyle
            .Elements(Presentation + "Setter")
            .Single(element => string.Equals(
                element.Attribute("Property")?.Value,
                "Padding",
                StringComparison.Ordinal));
        var contextMenuTemplate = uniformContextMenuStyle
            .Descendants(Presentation + "ControlTemplate")
            .Single();
        var itemsHost = contextMenuTemplate
            .Descendants(Presentation + "StackPanel")
            .Single();

        Assert.Equal(previewHandler, dataGrid.Attribute("PreviewMouseRightButtonDown")?.Value);
        Assert.Equal(openingHandler, dataGrid.Attribute("ContextMenuOpening")?.Value);
        Assert.Equal(contextMenuName, contextMenu.Attribute(Xaml + "Name")?.Value);
        Assert.Null(contextMenu.Attribute("Opened"));
        Assert.Null(contextMenu.Attribute("Closed"));
        Assert.Equal("240", contextMenu.Attribute("MinWidth")?.Value);
        Assert.Equal(
            "{StaticResource IMM.UniformContextMenuStyle}",
            contextMenu.Attribute("Style")?.Value);
        Assert.Equal("0", paddingSetter.Attribute("Value")?.Value);
        Assert.Equal("True", itemsHost.Attribute("IsItemsHost")?.Value);
        Assert.Empty(contextMenuTemplate.Descendants(Presentation + "ScrollViewer"));
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

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        var candidates = new[]
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
            Path.GetDirectoryName(sourceFilePath)
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;

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
