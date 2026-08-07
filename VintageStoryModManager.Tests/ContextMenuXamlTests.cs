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

    [Fact]
    public void ModlistGroupReferencesAreClickableAndExposeOnlyTheirTwoActions()
    {
        var document = LoadMainWindow();
        var referenceList = document
            .Descendants(Presentation + "ItemsControl")
            .Single(element => string.Equals(
                element.Attribute("ItemsSource")?.Value,
                "{Binding GroupMembers}",
                StringComparison.Ordinal));
        var referenceButton = referenceList
            .Descendants(Presentation + "Button")
            .Single();
        var contextMenu = referenceButton
            .Elements(Presentation + "Button.ContextMenu")
            .Elements(Presentation + "ContextMenu")
            .Single();
        var menuItems = contextMenu.Elements(Presentation + "MenuItem").ToList();
        var localGrid = document
            .Descendants(Presentation + "DataGrid")
            .Single(element => string.Equals(
                element.Attribute(Xaml + "Name")?.Value,
                "LocalModlistsDataGrid",
                StringComparison.Ordinal));
        var expander = localGrid
            .Descendants(Presentation + "ToggleButton")
            .Single(element => string.Equals(
                element.Attribute("Click")?.Value,
                "ModlistGroupExpanderButton_OnClick",
                StringComparison.Ordinal));
        var rowDetailsBorder = localGrid
            .Elements(Presentation + "DataGrid.RowDetailsTemplate")
            .Elements(Presentation + "DataTemplate")
            .Elements(Presentation + "Border")
            .Single();
        var expanderBorderThickness = expander
            .Descendants(Presentation + "Setter")
            .Single(setter => string.Equals(
                setter.Attribute("Property")?.Value,
                "BorderThickness",
                StringComparison.Ordinal));
        var duplicateModsButton = document
            .Descendants(Presentation + "Button")
            .Single(element => string.Equals(
                element.Attribute(Xaml + "Name")?.Value,
                "FixDuplicateModsButton",
                StringComparison.Ordinal));

        Assert.Equal("GroupReferenceItemButton_OnClick", referenceButton.Attribute("Click")?.Value);
        Assert.Null(referenceButton.Attribute("PreviewMouseRightButtonDown"));
        Assert.DoesNotContain(document.Descendants(Presentation + "ListBox"), element => string.Equals(
                element.Attribute("ItemsSource")?.Value,
                "{Binding GroupMembers}",
                StringComparison.Ordinal));
        Assert.Equal(2, menuItems.Count);
        Assert.Equal("240", contextMenu.Attribute("MinWidth")?.Value);
        Assert.Equal("Remove from group", menuItems[0].Attribute("Header")?.Value);
        Assert.Equal("Select modlist", menuItems[1].Attribute("Header")?.Value);
        Assert.All(menuItems, item => Assert.Equal("32", item.Attribute("MinHeight")?.Value));
        Assert.All(menuItems, item => Assert.Equal(
            "{StaticResource ToggleMenuItemStyle}",
            item.Attribute("Style")?.Value));
        Assert.Equal("Collapsed", localGrid.Attribute("RowDetailsVisibilityMode")?.Value);
        Assert.Equal("LocalModlistsDataGrid_OnLoadingRow", localGrid.Attribute("LoadingRow")?.Value);
        Assert.Equal("{Binding IsGroupExpanded, Mode=TwoWay}", expander.Attribute("IsChecked")?.Value);
        Assert.Equal("Right", expander.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("32", expander.Attribute("Width")?.Value);
        Assert.Equal(2, expander.Descendants(Presentation + "Path").Count());
        Assert.Empty(expander.Descendants(Presentation + "Border"));
        Assert.Equal("0", expanderBorderThickness.Attribute("Value")?.Value);
        Assert.Equal("22,4,22,4", rowDetailsBorder.Attribute("Margin")?.Value);
        Assert.Equal("4,0", rowDetailsBorder.Attribute("Padding")?.Value);
        Assert.Equal("Fix duplicate mods (0)", duplicateModsButton.Attribute("Content")?.Value);
        Assert.Equal("FixDuplicateModsButton_OnClick", duplicateModsButton.Attribute("Click")?.Value);
        Assert.DoesNotContain(document.Descendants(Presentation + "DataTrigger"), trigger => string.Equals(
            trigger.Attribute("Binding")?.Value,
            "{Binding HasUnresolvedConflicts}",
            StringComparison.Ordinal));
    }

    [Fact]
    public void LocalModlistContextMenuContainsOnlyActionsForItsSelection()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "VintageStoryModManager",
            "Views",
            "MainWindow.xaml.cs"));
        var methodStart = source.IndexOf(
            "private void LocalModlistsDataGrid_OnContextMenuOpening",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "private void CloudModlistsDataGrid_OnPreviewMouseRightButtonDown",
            methodStart,
            StringComparison.Ordinal);
        var method = source[methodStart..methodEnd];

        Assert.Contains("if (selectionCount == 0)", method, StringComparison.Ordinal);
        Assert.Contains("e.Handled = true", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Save current mods as local modlist", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Create modlist group", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Open modlists folder", method, StringComparison.Ordinal);
        Assert.Contains("contextMenu.Placement = PlacementMode.MousePoint", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalModlistModsDropdownMatchesLaunchButtonWidth()
    {
        var document = LoadMainWindow();
        var launchButton = document
            .Descendants(Presentation + "Button")
            .Single(element => string.Equals(
                element.Attribute(Xaml + "Name")?.Value,
                "LaunchGameButton",
                StringComparison.Ordinal));
        var localModsComboBox = document
            .Descendants(Presentation + "ComboBox")
            .Single(element => string.Equals(
                element.Attribute(Xaml + "Name")?.Value,
                "LocalModsComboBox",
                StringComparison.Ordinal));
        var dropdownContainer = Assert.IsType<XElement>(localModsComboBox.Parent);

        Assert.Equal(launchButton.Attribute("MinWidth")?.Value, dropdownContainer.Attribute("Width")?.Value);
        Assert.Equal("Left", dropdownContainer.Attribute("HorizontalAlignment")?.Value);
    }

    [Fact]
    public void SelectingReferencedModlistPreservesHorizontalScrollPosition()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "VintageStoryModManager",
            "Views",
            "MainWindow.xaml.cs"));
        var methodStart = source.IndexOf(
            "private void SelectReferencedModlist",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "private void LocalModlistsDataGrid_OnPreviewMouseLeftButtonDown",
            methodStart,
            StringComparison.Ordinal);
        var method = source[methodStart..methodEnd];

        Assert.Contains("previousHorizontalOffset", method, StringComparison.Ordinal);
        Assert.Contains("ScrollIntoView(sourceEntry)", method, StringComparison.Ordinal);
        Assert.Contains(
            "ScrollToHorizontalOffset(previousHorizontalOffset)",
            method,
            StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.Loaded", method, StringComparison.Ordinal);
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
