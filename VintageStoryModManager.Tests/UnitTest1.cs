using System.Collections.Generic;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.Tests;

public class ModVersionVoteAggregatorTests
{
    [Fact]
    public void CreateSummary_AggregatesCountsAndRecentComments()
    {
        var votes = new Dictionary<string, ModVersionVoteRecord>
        {
            ["user1"] = new() { Option = ModVersionVoteOption.FullyFunctional },
            ["user2"] = new()
            {
                Option = ModVersionVoteOption.NotFunctional,
                Comment = "Broken in latest build",
                UpdatedUtc = "2024-01-02T10:00:00Z"
            },
            ["user3"] = new()
            {
                Option = ModVersionVoteOption.CrashesOrFreezesGame,
                Comment = "Crash on startup",
                UpdatedUtc = "2024-01-03T10:00:00Z"
            },
            ["user4"] = new()
            {
                Option = ModVersionVoteOption.NotFunctional,
                Comment = "Older issue",
                UpdatedUtc = "2024-01-01T00:00:00Z"
            }
        };

        var summary = ModVersionVoteAggregator.CreateSummary(votes, 2, "user3");

        Assert.Equal(1, summary.FullyFunctional);
        Assert.Equal(0, summary.NoIssuesSoFar);
        Assert.Equal(0, summary.SomeIssuesButWorks);
        Assert.Equal(2, summary.NotFunctional);
        Assert.Equal(1, summary.CrashesOrFreezesGame);
        Assert.Equal("user3", summary.UserVoteId);

        Assert.Collection(
            summary.RecentComments.NotFunctional,
            comment => Assert.Equal("Broken in latest build", comment.Text),
            comment => Assert.Equal("Older issue", comment.Text));

        Assert.Collection(
            summary.RecentComments.CrashesOrFreezesGame,
            comment => Assert.Equal("Crash on startup", comment.Text));
    }

    [Fact]
    public void ApplyVoteChange_KeepsCountsConsistentForUpdatesAndRemovals()
    {
        var startingVotes = new Dictionary<string, ModVersionVoteRecord>
        {
            ["user1"] = new()
            {
                Option = ModVersionVoteOption.NotFunctional,
                Comment = "Fails to load",
                UpdatedUtc = "2024-01-02T00:00:00Z"
            },
            ["user2"] = new() { Option = ModVersionVoteOption.FullyFunctional }
        };

        var summary = ModVersionVoteAggregator.CreateSummary(startingVotes, 5, null);

        var updatedVote = new ModVersionVoteRecord
        {
            Option = ModVersionVoteOption.NoIssuesSoFar,
            UpdatedUtc = "2024-01-04T00:00:00Z"
        };

        summary = ModVersionVoteAggregator.ApplyVoteChange(summary, startingVotes["user1"], updatedVote, "user1", 5);

        Assert.Equal(1, summary.FullyFunctional);
        Assert.Equal(1, summary.NoIssuesSoFar);
        Assert.Equal(0, summary.NotFunctional);
        Assert.Empty(summary.RecentComments.NotFunctional);

        var removalSummary = ModVersionVoteAggregator.ApplyVoteChange(summary, updatedVote, null, "user1", 5);

        Assert.Equal(1, removalSummary.FullyFunctional);
        Assert.Equal(0, removalSummary.NoIssuesSoFar);
        Assert.Empty(removalSummary.RecentComments.NotFunctional);
        Assert.Equal("user1", removalSummary.UserVoteId);
    }
}
