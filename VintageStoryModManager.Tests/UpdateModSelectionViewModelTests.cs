using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class UpdateModSelectionViewModelTests
{
    [Fact]
    public void ChangelogsStopAtTargetCompatibleRelease()
    {
        var incompatibleLatest = CreateRelease("3.0.0", "latest incompatible changelog", false);
        var compatibleTarget = CreateRelease("2.0.0", "target compatible changelog", true);
        var installedRelease = CreateRelease("1.0.0", "installed changelog", true);
        var mod = CreateModViewModel(
            installedVersion: "1.0.0",
            latestRelease: incompatibleLatest,
            latestCompatibleRelease: compatibleTarget,
            releases: [incompatibleLatest, compatibleTarget, installedRelease]);
        var selection = new UpdateModSelectionViewModel(mod, true, null, false);

        Assert.Equal(["2.0.0"], selection.Changelogs.Select(changelog => changelog.Version));
        Assert.Equal("target compatible changelog", selection.Changelogs.Single().Changelog);
    }

    private static ModListItemViewModel CreateModViewModel(
        string installedVersion,
        ModReleaseInfo latestRelease,
        ModReleaseInfo latestCompatibleRelease,
        IReadOnlyList<ModReleaseInfo> releases)
    {
        var entry = new ModEntry
        {
            ModId = "testmod",
            Name = "Test Mod",
            Version = installedVersion,
            SourceKind = ModSourceKind.ZipArchive,
            DatabaseInfo = new ModDatabaseInfo
            {
                LatestRelease = latestRelease,
                LatestCompatibleRelease = latestCompatibleRelease,
                LatestCompatibleVersion = latestCompatibleRelease.Version,
                Releases = releases
            }
        };

        return new ModListItemViewModel(
            entry,
            isActive: true,
            location: "Mods",
            activationHandler: (_, _) => Task.FromResult(new ActivationResult(true, null)),
            installedGameVersion: "1.20",
            isInstalled: true,
            shouldSkipVersion: null,
            requireExactVersionMatch: () => true,
            initializeUserReportState: false);
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
