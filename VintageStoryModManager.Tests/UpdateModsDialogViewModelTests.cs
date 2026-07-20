using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class UpdateModsDialogViewModelTests
{
    [Fact]
    public void BuildChangelogClipboardTextIncludesEntireDisplayedList()
    {
        var alpha = CreateSelection(
            displayName: "Alpha Mod",
            modId: "alpha",
            installedVersion: "1.0.0",
            targetRelease: CreateRelease("2.0.0", "Added new blocks.", true),
            isSelected: true);
        var beta = CreateSelection(
            displayName: "Beta Mod",
            modId: "beta",
            installedVersion: "0.9.0",
            targetRelease: CreateRelease("1.0.0", "Fixed crashes.\nImproved balance.", true),
            isSelected: false);
        var viewModel = new UpdateModsDialogViewModel([alpha, beta]);

        var text = viewModel.BuildChangelogClipboardText();

        var expected = string.Join(Environment.NewLine,
            "Alpha Mod",
            "Installed: 1.0.0 \u2192 Update to: 2.0.0",
            "Changelog",
            "2.0.0",
            "Added new blocks.",
            "",
            "Beta Mod",
            "Installed: 0.9.0 \u2192 Update to: 1.0.0",
            "Changelog",
            "1.0.0",
            "Fixed crashes.",
            "Improved balance.");
        Assert.Equal(expected, text);
    }

    private static UpdateModSelectionViewModel CreateSelection(
        string displayName,
        string modId,
        string installedVersion,
        ModReleaseInfo targetRelease,
        bool isSelected)
    {
        var installedRelease = CreateRelease(installedVersion, "Installed release.", true);
        var entry = new ModEntry
        {
            ModId = modId,
            Name = displayName,
            Version = installedVersion,
            SourceKind = ModSourceKind.ZipArchive,
            DatabaseInfo = new ModDatabaseInfo
            {
                LatestRelease = targetRelease,
                LatestCompatibleRelease = targetRelease,
                LatestCompatibleVersion = targetRelease.Version,
                Releases = [targetRelease, installedRelease]
            }
        };

        var mod = new ModListItemViewModel(
            entry,
            isActive: true,
            location: "Mods",
            activationHandler: (_, _) => Task.FromResult(new ActivationResult(true, null)),
            installedGameVersion: "1.20",
            isInstalled: true,
            shouldSkipVersion: null,
            requireExactVersionMatch: () => true,
            initializeUserReportState: false);

        return new UpdateModSelectionViewModel(mod, isSelected, null, false);
    }

    private static ModReleaseInfo CreateRelease(string version, string changelog, bool isCompatible)
    {
        return new ModReleaseInfo
        {
            Version = version,
            NormalizedVersion = version,
            DownloadUri = new Uri($"https://example.invalid/{version}.zip"),
            FileName = $"{version}.zip",
            GameVersionTags = isCompatible ? ["1.20"] : ["1.21"],
            IsCompatibleWithInstalledGame = isCompatible,
            Changelog = changelog,
            CreatedUtc = DateTime.UtcNow
        };
    }
}
