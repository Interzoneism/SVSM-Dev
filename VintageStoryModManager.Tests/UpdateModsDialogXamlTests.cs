using System.Xml.Linq;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class UpdateModsDialogXamlTests
{
    [Fact]
    public void DisplayNameBindingIsOneWay()
    {
        var xamlPath = Path.Combine(
            FindRepositoryRoot(),
            "VintageStoryModManager",
            "Views",
            "Dialogs",
            "UpdateModsDialog.xaml");

        var document = XDocument.Load(Path.GetFullPath(xamlPath));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var displayNameRun = document
            .Descendants(presentation + "Run")
            .Single(element => string.Equals(
                element.Attribute("Text")?.Value,
                "{Binding DisplayName, Mode=OneWay}",
                StringComparison.Ordinal));

        Assert.NotNull(displayNameRun);
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
