using System;

namespace VintageStoryModManager.Models;

/// <summary>
/// Represents the available user report options for a mod version.
/// </summary>
public enum ModVersionVoteOption
{
    WorkingPerfectly,
    SomeIssuesButWorks,
    NotWorking
}

/// <summary>
/// Aggregated vote counts for each <see cref="ModVersionVoteOption"/> value.
/// </summary>
public sealed class ModVersionVoteCounts
{
    public ModVersionVoteCounts(int workingPerfectly, int someIssuesButWorks, int notWorking)
    {
        WorkingPerfectly = Math.Max(0, workingPerfectly);
        SomeIssuesButWorks = Math.Max(0, someIssuesButWorks);
        NotWorking = Math.Max(0, notWorking);
    }

    public int WorkingPerfectly { get; }

    public int SomeIssuesButWorks { get; }

    public int NotWorking { get; }

    public int Total => WorkingPerfectly + SomeIssuesButWorks + NotWorking;

    public int GetCount(ModVersionVoteOption option) => option switch
    {
        ModVersionVoteOption.WorkingPerfectly => WorkingPerfectly,
        ModVersionVoteOption.SomeIssuesButWorks => SomeIssuesButWorks,
        ModVersionVoteOption.NotWorking => NotWorking,
        _ => 0
    };

    public static ModVersionVoteCounts Empty { get; } = new(0, 0, 0);
}

/// <summary>
/// Captures the user report summary for a mod version at a particular Vintage Story version.
/// </summary>
public sealed class ModVersionVoteSummary
{
    public ModVersionVoteSummary(
        string modId,
        string modVersion,
        string? vintageStoryVersion,
        ModVersionVoteCounts counts,
        ModVersionVoteOption? userVote)
    {
        ModId = modId ?? throw new ArgumentNullException(nameof(modId));
        ModVersion = modVersion ?? throw new ArgumentNullException(nameof(modVersion));
        VintageStoryVersion = vintageStoryVersion;
        Counts = counts ?? throw new ArgumentNullException(nameof(counts));
        UserVote = userVote;
    }

    public string ModId { get; }

    public string ModVersion { get; }

    public string? VintageStoryVersion { get; }

    public ModVersionVoteCounts Counts { get; }

    public ModVersionVoteOption? UserVote { get; }

    public int TotalVotes => Counts.Total;

    public ModVersionVoteOption? GetMajorityOption()
    {
        int working = Counts.WorkingPerfectly;
        int issues = Counts.SomeIssuesButWorks;
        int failing = Counts.NotWorking;

        int max = Math.Max(working, Math.Max(issues, failing));
        if (max == 0)
        {
            return null;
        }

        int duplicates = 0;
        ModVersionVoteOption? candidate = null;

        if (working == max)
        {
            duplicates++;
            candidate = ModVersionVoteOption.WorkingPerfectly;
        }

        if (issues == max)
        {
            duplicates++;
            candidate = ModVersionVoteOption.SomeIssuesButWorks;
        }

        if (failing == max)
        {
            duplicates++;
            candidate = ModVersionVoteOption.NotWorking;
        }

        return duplicates == 1 ? candidate : null;
    }
}

public static class ModVersionVoteOptionExtensions
{
    public static string ToDisplayString(this ModVersionVoteOption option) => option switch
    {
        ModVersionVoteOption.WorkingPerfectly => "Working Perfectly",
        ModVersionVoteOption.SomeIssuesButWorks => "Some Issues But Works",
        ModVersionVoteOption.NotWorking => "Not Working",
        _ => option.ToString() ?? string.Empty
    };
}
