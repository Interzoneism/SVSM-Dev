using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class TargetGameVersionReleaseSelectorTests
{
    [Fact]
    public void SelectLatestCompatibleReleaseReturnsNewestReleaseMarkedForTarget()
    {
        var latestIncompatible = CreateRelease("3.0.0", "1.21");
        var newestCompatible = CreateRelease("2.0.0", "1.20");
        var olderCompatible = CreateRelease("1.0.0", "1.20");

        var selected = TargetGameVersionReleaseSelector.SelectLatestCompatibleRelease(
            [latestIncompatible, newestCompatible, olderCompatible],
            "1.20",
            requireExactVersionMatch: true);

        Assert.Same(newestCompatible, selected);
    }

    [Fact]
    public void SelectLatestCompatibleReleaseDoesNotFallbackToUntaggedReleaseWhenTargetKnown()
    {
        var untaggedLatest = CreateRelease("3.0.0");
        var taggedCompatible = CreateRelease("2.0.0", "1.20");

        var selected = TargetGameVersionReleaseSelector.SelectLatestCompatibleRelease(
            [untaggedLatest, taggedCompatible],
            "1.20",
            requireExactVersionMatch: true);

        Assert.Same(taggedCompatible, selected);
    }

    [Fact]
    public void SelectLatestCompatibleReleaseReturnsNullWhenNoReleaseIsMarkedForTarget()
    {
        var latestIncompatible = CreateRelease("3.0.0", "1.21");
        var untagged = CreateRelease("2.0.0");

        var selected = TargetGameVersionReleaseSelector.SelectLatestCompatibleRelease(
            [latestIncompatible, untagged],
            "1.20",
            requireExactVersionMatch: true);

        Assert.Null(selected);
    }

    [Fact]
    public void SelectLatestCompatibleReleaseReturnsNewestReleaseWhenTargetIsLatest()
    {
        var latestRelease = CreateRelease("3.0.0", "1.21");
        var olderRelease = CreateRelease("2.0.0", "1.20");

        var selected = TargetGameVersionReleaseSelector.SelectLatestCompatibleRelease(
            [latestRelease, olderRelease],
            TargetGameVersionPreference.ResolveEffectiveGameVersion("1.20", TargetGameVersionPreference.Latest),
            requireExactVersionMatch: true);

        Assert.Same(latestRelease, selected);
    }

    private static ModReleaseInfo CreateRelease(string version, params string[] gameVersionTags)
    {
        return new ModReleaseInfo
        {
            Version = version,
            NormalizedVersion = version,
            DownloadUri = new Uri($"https://example.invalid/{version}.zip"),
            FileName = $"{version}.zip",
            GameVersionTags = gameVersionTags,
            IsCompatibleWithInstalledGame = false
        };
    }
}
