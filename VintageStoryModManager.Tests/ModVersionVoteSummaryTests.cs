using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModVersionVoteSummaryTests
{
    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 0)]
    [InlineData(5, 4)]
    public void PositiveMajorityWithMoreThanThreeVotesSuppressesCompatibilityWarning(
        int fullyFunctional,
        int noIssues)
    {
        var summary = CreateSummary(fullyFunctional, noIssues, notFunctional: 1);

        Assert.True(summary.HasPositiveCompatibilityConsensus());
    }

    [Theory]
    [InlineData(0, 3, 0)]
    [InlineData(3, 0, 0)]
    [InlineData(4, 0, 5)]
    [InlineData(0, 4, 5)]
    public void InsufficientOrNonMajorityPositiveVotesKeepCompatibilityWarning(
        int fullyFunctional,
        int noIssues,
        int notFunctional)
    {
        var summary = CreateSummary(fullyFunctional, noIssues, notFunctional);

        Assert.False(summary.HasPositiveCompatibilityConsensus());
    }

    [Fact]
    public void PositiveConsensusHidesOnlyTheInstalledVersionWarning()
    {
        var mod = CreateVersionMismatchedMod();
        Assert.Equal("Warning", mod.StatusText);

        mod.ApplyUserReportSummary(CreateSummary(0, 4, 0));

        Assert.Equal(string.Empty, mod.StatusText);

        mod.SetUserReportLoading();

        Assert.Equal("Warning", mod.StatusText);
    }

    [Fact]
    public void NegativeVotesExposeReasonDetailsState()
    {
        var mod = CreateVersionMismatchedMod();

        mod.ApplyUserReportSummary(CreateSummary(0, 0, 1));

        Assert.True(mod.HasNegativeUserReports);

        mod.SetUserReportLoading();

        Assert.False(mod.HasNegativeUserReports);
    }

    private static ModVersionVoteSummary CreateSummary(
        int fullyFunctional,
        int noIssues,
        int notFunctional)
    {
        return new ModVersionVoteSummary(
            "testmod",
            "1.0.0",
            "1.21.0",
            new ModVersionVoteCounts(fullyFunctional, noIssues, 0, notFunctional, 0),
            ModVersionVoteComments.Empty,
            null,
            null);
    }

    private static ModListItemViewModel CreateVersionMismatchedMod()
    {
        ModEntry entry = new()
        {
            ModId = "testmod",
            Name = "Test Mod",
            Version = "1.0.0",
            Dependencies = [new ModDependencyInfo("game", "1.20.0")],
            SourceKind = ModSourceKind.ZipArchive
        };

        return new ModListItemViewModel(
            entry,
            isActive: true,
            location: "Mods",
            activationHandler: (_, _) => Task.FromResult(new ActivationResult(true, null)),
            installedGameVersion: "1.21.0",
            isInstalled: true,
            initializeUserReportState: false);
    }
}
