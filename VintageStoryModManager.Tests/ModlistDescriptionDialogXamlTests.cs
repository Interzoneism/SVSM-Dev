using System.Xml.Linq;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModlistDescriptionDialogXamlTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData("CloudModlistDetailsDialog.xaml")]
    [InlineData("LocalModlistEditDialog.xaml")]
    public void DescriptionEditorSupportsLongScrollableText(string dialogFileName)
    {
        var dialogPath = Path.Combine(
            FindRepositoryRoot(),
            "VintageStoryModManager",
            "Views",
            "Dialogs",
            dialogFileName);
        var document = XDocument.Load(Path.GetFullPath(dialogPath));
        var descriptionTextBox = document
            .Descendants(Presentation + "TextBox")
            .Single(element => string.Equals(
                element.Attribute(Xaml + "Name")?.Value,
                "DescriptionTextBox",
                StringComparison.Ordinal));

        Assert.Equal("570", descriptionTextBox.Attribute("MaxLength")?.Value);
        Assert.Equal("160", descriptionTextBox.Attribute("Height")?.Value);
        Assert.Equal("Wrap", descriptionTextBox.Attribute("TextWrapping")?.Value);
        Assert.Equal("True", descriptionTextBox.Attribute("AcceptsReturn")?.Value);
        Assert.Equal("Disabled", descriptionTextBox.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Equal("Auto", descriptionTextBox.Attribute("VerticalScrollBarVisibility")?.Value);
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

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
