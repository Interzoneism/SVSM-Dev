using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace ImprovedModMenu.Tests;

public static class XamlTestHelper
{
    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XName AutomationIdName = XName.Get("AutomationProperties.AutomationId");
    private static readonly XName NamespacedAutomationIdName = PresentationNamespace + "AutomationProperties.AutomationId";
    private static readonly XName AutomationName = XName.Get("AutomationProperties.Name");
    private static readonly XName NamespacedAutomationName = PresentationNamespace + "AutomationProperties.Name";

    public static XDocument LoadXaml(string relativePath)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var fullPath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Expected XAML file at {fullPath}");
        return XDocument.Load(fullPath);
    }

    private static string? GetAutomationId(XElement element) =>
        element.Attribute(NamespacedAutomationIdName)?.Value ?? element.Attribute(AutomationIdName)?.Value;

    private static string? GetAutomationName(XElement element) =>
        element.Attribute(NamespacedAutomationName)?.Value ?? element.Attribute(AutomationName)?.Value;

    public static bool HasAutomationId(XDocument document, string automationId) =>
        document.Descendants()
                .Select(GetAutomationId)
                .Any(value => string.Equals(value, automationId, StringComparison.Ordinal));

    public static bool HasAutomationIdContaining(XDocument document, string substring) =>
        document.Descendants()
                .Select(GetAutomationId)
                .Any(value => value?.Contains(substring, StringComparison.Ordinal) == true);

    public static bool HasAutomationNameContaining(XDocument document, string substring) =>
        document.Descendants()
                .Select(GetAutomationName)
                .Any(value => value?.Contains(substring, StringComparison.Ordinal) == true);
}

public class AutomationMetadataTests
{
    [Fact]
    public void MainWindow_HasExpectedFixedAutomationIds()
    {
        var document = XamlTestHelper.LoadXaml(Path.Combine("ImprovedModMenu", "Views", "MainWindow.xaml"));
        var expectedIds = new[]
        {
            "MainMenu",
            "FileMenu",
            "RefreshModsMenuItem",
            "SetDataFolderMenuItem",
            "SetGameFolderMenuItem",
            "ExitMenuItem",
            "HelpMenu",
            "AboutMenuItem",
            "ModsDataGrid",
            "LaunchGameButton",
            "OpenModFolderButton",
            "OpenConfigFolderButton",
            "OpenLogsFolderButton",
            "PresetComboBox",
            "SavePresetButton"
        };

        foreach (var automationId in expectedIds)
        {
            Assert.True(
                XamlTestHelper.HasAutomationId(document, automationId),
                $"Expected to find an element in MainWindow.xaml with AutomationProperties.AutomationId='{automationId}'.");
        }
    }

    [Fact]
    public void MainWindow_DataGridTemplatesExposeAutomationIds()
    {
        var document = XamlTestHelper.LoadXaml(Path.Combine("ImprovedModMenu", "Views", "MainWindow.xaml"));
        var expectedSubstrings = new[]
        {
            "ModActivationToggle_",
            "OpenModDatabasePageButton_",
            "EditConfigButton_",
            "DeleteModButton_"
        };

        foreach (var substring in expectedSubstrings)
        {
            Assert.True(
                XamlTestHelper.HasAutomationIdContaining(document, substring),
                $"Expected to find an AutomationProperties.AutomationId containing '{substring}' in MainWindow.xaml.");
        }
    }

    [Fact]
    public void MainWindow_DataGridTemplatesExposeAutomationNames()
    {
        var document = XamlTestHelper.LoadXaml(Path.Combine("ImprovedModMenu", "Views", "MainWindow.xaml"));
        var expectedSubstrings = new[]
        {
            "Toggle Activation for",
            "Open Mod Database Page for",
            "Edit Config for",
            "Delete "
        };

        foreach (var substring in expectedSubstrings)
        {
            Assert.True(
                XamlTestHelper.HasAutomationNameContaining(document, substring),
                $"Expected to find an AutomationProperties.Name containing '{substring}' in MainWindow.xaml.");
        }
    }

    [Fact]
    public void ModConfigEditorWindow_HasAutomationMetadataForKeyControls()
    {
        var document = XamlTestHelper.LoadXaml(Path.Combine("ImprovedModMenu", "Views", "ModConfigEditorWindow.xaml"));
        var expectedIds = new[]
        {
            "ModConfigTreeView",
            "ConfigSaveButton",
            "ConfigCancelButton"
        };

        foreach (var automationId in expectedIds)
        {
            Assert.True(
                XamlTestHelper.HasAutomationId(document, automationId),
                $"Expected to find an element in ModConfigEditorWindow.xaml with AutomationProperties.AutomationId='{automationId}'.");
        }

        Assert.True(
            XamlTestHelper.HasAutomationNameContaining(document, "{Binding DisplayName}"),
            "Expected value editors in ModConfigEditorWindow.xaml to expose AutomationProperties.Name bound to the display name.");
    }
}
