using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

internal static class ModVersionVoteAggregator
{
    public static VoteSummaryRecord CreateSummary(
        IReadOnlyDictionary<string, ModVersionVoteRecord> votes,
        int maxRecentComments,
        string? updatedByUserId)
    {
        var summary = new VoteSummaryRecord { UserVoteId = updatedByUserId };

        foreach (var (userId, vote) in votes)
        {
            ApplyVoteToCounts(summary, vote.Option, +1);
            AppendComment(summary, vote, userId, maxRecentComments);
        }

        SortAndTrimComments(summary, maxRecentComments);
        return summary;
    }

    public static VoteSummaryRecord ApplyVoteChange(
        VoteSummaryRecord? existing,
        ModVersionVoteRecord? previousVote,
        ModVersionVoteRecord? newVote,
        string userId,
        int maxRecentComments)
    {
        var summary = CloneOrCreate(existing);

        if (previousVote is not null)
            ApplyVoteToCounts(summary, previousVote.Option, -1);

        if (newVote is not null)
            ApplyVoteToCounts(summary, newVote.Option, +1);

        UpdateComments(summary, previousVote, newVote, userId, maxRecentComments);

        summary.UserVoteId = userId;
        summary.UpdatedUtc = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        return summary;
    }

    public static ModVersionVoteComments ToDomainComments(VoteSummaryRecord? summary)
    {
        if (summary is null) return ModVersionVoteComments.Empty;

        return new ModVersionVoteComments(
            summary.RecentComments.NotFunctional.Select(c => c.Text).ToArray(),
            summary.RecentComments.CrashesOrFreezesGame.Select(c => c.Text).ToArray());
    }

    private static void UpdateComments(
        VoteSummaryRecord summary,
        ModVersionVoteRecord? previousVote,
        ModVersionVoteRecord? newVote,
        string userId,
        int maxRecentComments)
    {
        RemoveExistingUserComments(summary, userId);

        AppendComment(summary, newVote, userId, maxRecentComments);

        SortAndTrimComments(summary, maxRecentComments);
    }

    private static void RemoveExistingUserComments(VoteSummaryRecord summary, string userId)
    {
        summary.RecentComments.NotFunctional.RemoveAll(c => string.Equals(c.UserId, userId, StringComparison.Ordinal));
        summary.RecentComments.CrashesOrFreezesGame.RemoveAll(c => string.Equals(c.UserId, userId, StringComparison.Ordinal));
    }

    private static void AppendComment(
        VoteSummaryRecord summary,
        ModVersionVoteRecord? vote,
        string userId,
        int maxRecentComments)
    {
        if (vote is null) return;

        var normalizedComment = NormalizeComment(vote.Comment);
        var targetList = vote.Option switch
        {
            ModVersionVoteOption.NotFunctional => summary.RecentComments.NotFunctional,
            ModVersionVoteOption.CrashesOrFreezesGame => summary.RecentComments.CrashesOrFreezesGame,
            _ => null
        };

        if (targetList is null || string.IsNullOrEmpty(normalizedComment)) return;

        targetList.Add(new VoteSummaryComment
        {
            Text = normalizedComment,
            UpdatedUtc = NormalizeTimestamp(vote.UpdatedUtc),
            UserId = userId
        });

        if (targetList.Count > maxRecentComments)
            SortAndTrim(targetList, maxRecentComments);
    }

    private static void SortAndTrimComments(VoteSummaryRecord summary, int maxRecentComments)
    {
        SortAndTrim(summary.RecentComments.NotFunctional, maxRecentComments);
        SortAndTrim(summary.RecentComments.CrashesOrFreezesGame, maxRecentComments);
    }

    private static void SortAndTrim(List<VoteSummaryComment> comments, int maxRecentComments)
    {
        comments.Sort((left, right) =>
            GetUpdatedTicks(right.UpdatedUtc).CompareTo(GetUpdatedTicks(left.UpdatedUtc)));

        if (comments.Count > maxRecentComments)
            comments.RemoveRange(maxRecentComments, comments.Count - maxRecentComments);
    }

    private static long GetUpdatedTicks(string? updatedUtc)
    {
        return DateTimeOffset.TryParse(updatedUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed.UtcTicks
            : DateTimeOffset.MinValue.UtcTicks;
    }

    private static VoteSummaryRecord CloneOrCreate(VoteSummaryRecord? existing)
    {
        if (existing is null)
            return new VoteSummaryRecord();

        return new VoteSummaryRecord
        {
            FullyFunctional = Math.Max(0, existing.FullyFunctional),
            NoIssuesSoFar = Math.Max(0, existing.NoIssuesSoFar),
            SomeIssuesButWorks = Math.Max(0, existing.SomeIssuesButWorks),
            NotFunctional = Math.Max(0, existing.NotFunctional),
            CrashesOrFreezesGame = Math.Max(0, existing.CrashesOrFreezesGame),
            UpdatedUtc = existing.UpdatedUtc,
            UserVoteId = existing.UserVoteId,
            RecentComments = new VoteSummaryComments
            {
                NotFunctional = new List<VoteSummaryComment>(existing.RecentComments.NotFunctional),
                CrashesOrFreezesGame = new List<VoteSummaryComment>(existing.RecentComments.CrashesOrFreezesGame)
            }
        };
    }

    private static void ApplyVoteToCounts(VoteSummaryRecord summary, ModVersionVoteOption option, int delta)
    {
        switch (option)
        {
            case ModVersionVoteOption.FullyFunctional:
                summary.FullyFunctional = Math.Max(0, summary.FullyFunctional + delta);
                break;
            case ModVersionVoteOption.NoIssuesSoFar:
                summary.NoIssuesSoFar = Math.Max(0, summary.NoIssuesSoFar + delta);
                break;
            case ModVersionVoteOption.SomeIssuesButWorks:
                summary.SomeIssuesButWorks = Math.Max(0, summary.SomeIssuesButWorks + delta);
                break;
            case ModVersionVoteOption.NotFunctional:
                summary.NotFunctional = Math.Max(0, summary.NotFunctional + delta);
                break;
            case ModVersionVoteOption.CrashesOrFreezesGame:
                summary.CrashesOrFreezesGame = Math.Max(0, summary.CrashesOrFreezesGame + delta);
                break;
        }
    }

    private static string? NormalizeTimestamp(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? NormalizeComment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}

internal sealed class VoteSummaryRecord
{
    [JsonPropertyName("fullyFunctional")] public int FullyFunctional { get; set; }

    [JsonPropertyName("noIssuesSoFar")] public int NoIssuesSoFar { get; set; }

    [JsonPropertyName("someIssuesButWorks")] public int SomeIssuesButWorks { get; set; }

    [JsonPropertyName("notFunctional")] public int NotFunctional { get; set; }

    [JsonPropertyName("crashesOrFreezesGame")] public int CrashesOrFreezesGame { get; set; }

    [JsonPropertyName("recentComments")] public VoteSummaryComments RecentComments { get; set; } = new();

    [JsonPropertyName("updatedUtc")] public string? UpdatedUtc { get; set; }

    [JsonPropertyName("userVoteId")] public string? UserVoteId { get; set; }
}

internal sealed class VoteSummaryComments
{
    [JsonPropertyName("notFunctional")] public List<VoteSummaryComment> NotFunctional { get; set; } = new();

    [JsonPropertyName("crashesOrFreezesGame")] public List<VoteSummaryComment> CrashesOrFreezesGame { get; set; } = new();
}

internal sealed class VoteSummaryComment
{
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;

    [JsonPropertyName("updatedUtc")] public string? UpdatedUtc { get; set; }

    [JsonPropertyName("userId")] public string UserId { get; set; } = string.Empty;
}
