using System.Xml.Linq;
using VintageStoryModManager.Views;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class MultiSelectDropdownXamlTests
{
    [Fact]
    public void PopupContentDoesNotReferenceRootWithXReferenceBindings()
    {
        var xamlPath = Path.Combine(
            FindRepositoryRoot(),
            "VintageStoryModManager",
            "Views",
            "MultiSelectDropdown.xaml");

        var document = XDocument.Load(Path.GetFullPath(xamlPath));

        var rootReferences = document
            .Descendants()
            .Attributes()
            .Where(attribute => attribute.Value.Contains("x:Reference Root", StringComparison.Ordinal))
            .Select(attribute => attribute.Value)
            .ToArray();

        Assert.Empty(rootReferences);
    }

    [Fact]
    public void CanConstructMultiSelectDropdownOnStaThread()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                _ = new MultiSelectDropdown();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    [Fact]
    public void ModBrowserTagsDropdownCapsOpenedMenuWidthAt300()
    {
        var xamlPath = Path.Combine(
            FindRepositoryRoot(),
            "VintageStoryModManager",
            "Views",
            "ModBrowserView.xaml");

        var document = XDocument.Load(Path.GetFullPath(xamlPath));
        XNamespace views = "clr-namespace:VintageStoryModManager.Views";

        var tagsDropdown = document
            .Descendants(views + "MultiSelectDropdown")
            .Single(element => string.Equals(
                element.Attribute("PlaceholderText")?.Value,
                "Tags",
                StringComparison.Ordinal));

        Assert.Equal("300", tagsDropdown.Attribute("MaxDropDownWidth")?.Value);
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
