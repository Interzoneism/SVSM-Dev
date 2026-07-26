using System.Xml.Linq;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModernUiStructureXamlTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace ModernWpf = "http://schemas.modernwpf.com/2019";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void ModDatabaseCardsUseCenteredVirtualizingRepeaterWithOriginalFootprint()
    {
        var document = LoadView("ModBrowserView.xaml");

        var repeater = document.Descendants(ModernWpf + "ItemsRepeater").Single();
        var scrollHost = repeater.Ancestors(ModernWpf + "ItemsRepeaterScrollHost").SingleOrDefault();
        var layout = document.Descendants(ModernWpf + "UniformGridLayout").Single(element =>
            string.Equals(element.Attribute(Xaml + "Key")?.Value, "ModCardsLayout", StringComparison.Ordinal));
        var card = repeater.Descendants(Presentation + "Border").Single(element =>
            string.Equals(element.Attribute(Xaml + "Name")?.Value, "ModCardBorder", StringComparison.Ordinal));

        Assert.NotNull(scrollHost);
        Assert.Equal("{StaticResource ModCardsLayout}", repeater.Attribute("Layout")?.Value);
        Assert.Equal("Center", layout.Attribute("ItemsJustification")?.Value);
        Assert.Equal("None", layout.Attribute("ItemsStretch")?.Value);
        Assert.Equal("360", layout.Attribute("MinItemWidth")?.Value);
        Assert.Equal("360", layout.Attribute("MinItemHeight")?.Value);
        Assert.Equal("344", card.Attribute("Width")?.Value);
        Assert.Equal("344", card.Attribute("Height")?.Value);
        Assert.Equal("8", card.Attribute("Margin")?.Value);
    }

    [Fact]
    public void InstalledModDetailsUsePurposeBuiltInlineMovablePane()
    {
        var document = LoadView("MainWindow.xaml");

        var splitView = document.Descendants(ModernWpf + "SplitView").Single(element =>
            string.Equals(element.Attribute(Xaml + "Name")?.Value, "InstalledModSplitView", StringComparison.Ordinal));
        var paneRoot = splitView.Descendants(Presentation + "Border").Single(element =>
            string.Equals(element.Attribute(Xaml + "Name")?.Value, "ModInfoPaneRoot", StringComparison.Ordinal));
        var placementButton = document.Descendants(Presentation + "Button").Single(element =>
            string.Equals(element.Attribute(Xaml + "Name")?.Value, "ModInfoPanePlacementButton", StringComparison.Ordinal));
        var versionSelector = paneRoot.Descendants(Presentation + "ComboBox").Single(element =>
            string.Equals(element.Attribute(Xaml + "Name")?.Value, "SelectedModVersionComboBox", StringComparison.Ordinal));
        var deleteButton = paneRoot.Descendants(Presentation + "Button").Single(element =>
            string.Equals(element.Attribute(Xaml + "Name")?.Value, "SelectedModDeleteButton", StringComparison.Ordinal));

        Assert.Equal("Inline", splitView.Attribute("DisplayMode")?.Value);
        Assert.Equal("372", splitView.Attribute("OpenPaneLength")?.Value);
        Assert.NotNull(paneRoot);
        Assert.NotNull(versionSelector);
        Assert.Equal("DeleteModButton_OnClick", deleteButton.Attribute("Click")?.Value);
        Assert.Equal("ModInfoPanePlacementButton_OnClick", placementButton.Attribute("Click")?.Value);
        Assert.DoesNotContain(document.Descendants(Presentation + "Canvas"), element =>
            string.Equals(element.Attribute(Xaml + "Name")?.Value, "ModInfoOverlay", StringComparison.Ordinal));
        Assert.DoesNotContain(document.Descendants(Presentation + "Border"), element =>
            string.Equals(element.Attribute(Xaml + "Name")?.Value, "MODINFO_border", StringComparison.Ordinal));
    }

    private static XDocument LoadView(string fileName)
    {
        var xamlPath = Path.Combine(
            FindRepositoryRoot(),
            "VintageStoryModManager",
            "Views",
            fileName);

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
                var solutionPath = Path.Combine(directory.FullName, "ImprovedModMenu.sln");
                if (File.Exists(solutionPath)) return directory.FullName;

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ImprovedModMenu.sln.");
    }
}
