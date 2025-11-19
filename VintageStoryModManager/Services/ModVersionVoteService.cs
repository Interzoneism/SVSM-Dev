using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SimpleVsManager.Cloud;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
///     Provides Firebase-backed storage for per-mod user report votes.
/// </summary>
public sealed class ModVersionVoteService
{
    private const int MaxRecentCommentsPerOption = 50;

    private static readonly string DefaultDbUrl = DevConfig.ModVersionVoteDefaultDbUrl;

    private static readonly string VotesRootPath = DevConfig.ModVersionVoteRootPath;

    private static readonly HttpClient HttpClient = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new ModVersionVoteOptionJsonConverter() }
    };

    private readonly FirebaseAnonymousAuthenticator _authenticator;

    private readonly string _dbUrl;

    public ModVersionVoteService()
        : this(DefaultDbUrl, new FirebaseAnonymousAuthenticator())
    {
    }

    public ModVersionVoteService(string databaseUrl, FirebaseAnonymousAuthenticator authenticator)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
            throw new ArgumentException("A Firebase database URL must be provided.", nameof(databaseUrl));

        _dbUrl = databaseUrl.TrimEnd('/');
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
    }


    public async Task<ModVersionVoteSummary> GetVoteSummaryAsync(
        string modId,
        string modVersion,
        string vintageStoryVersion,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(modId, modVersion, vintageStoryVersion);
        InternetAccessManager.ThrowIfInternetAccessDisabled();

        var session = await _authenticator
            .TryGetExistingSessionAsync(cancellationToken)
            .ConfigureAwait(false);

        var (Summary, _) = await GetVoteSummaryWithEtagAsync(
                session,
                modId,
                modVersion,
                vintageStoryVersion,
                cancellationToken)
            .ConfigureAwait(false);

        return Summary;
    }

    public async Task<VoteSummaryResult> GetVoteSummaryIfChangedAsync(
        string modId,
        string modVersion,
        string vintageStoryVersion,
        string? knownEtag,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(modId, modVersion, vintageStoryVersion);
        InternetAccessManager.ThrowIfInternetAccessDisabled();

        var session = await _authenticator
            .TryGetExistingSessionAsync(cancellationToken)
            .ConfigureAwait(false);

        return await GetVoteSummaryInternalAsync(
                session,
                modId,
                modVersion,
                vintageStoryVersion,
                knownEtag,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(ModVersionVoteSummary Summary, string? ETag)> GetVoteSummaryWithEtagAsync(
        string modId,
        string modVersion,
        string vintageStoryVersion,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(modId, modVersion, vintageStoryVersion);
        InternetAccessManager.ThrowIfInternetAccessDisabled();

        var session = await _authenticator
            .TryGetExistingSessionAsync(cancellationToken)
            .ConfigureAwait(false);

        return await GetVoteSummaryWithEtagAsync(
                session,
                modId,
                modVersion,
                vintageStoryVersion,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(ModVersionVoteSummary Summary, string? ETag)> SubmitVoteAsync(
        string modId,
        string modVersion,
        string vintageStoryVersion,
        ModVersionVoteOption option,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(modId, modVersion, vintageStoryVersion);
        InternetAccessManager.ThrowIfInternetAccessDisabled();

        var session = await _authenticator
            .GetSessionAsync(cancellationToken)
            .ConfigureAwait(false);

        var modKey = SanitizeKey(modId);
        var versionKey = SanitizeKey(modVersion);
        var userKey = session.UserId ??
                      throw new InvalidOperationException("Firebase session did not provide a user ID.");

        var existing = await FetchSummaryAsync(
                session,
                modKey,
                versionKey,
                null,
                true,
                cancellationToken)
            .ConfigureAwait(false);

        var record = new ModVersionVoteRecord
        {
            Option = option,
            VintageStoryVersion = vintageStoryVersion,
            UpdatedUtc = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Comment = NormalizeComment(comment)
        };

        var updatedSummary = ModVersionVoteAggregator.ApplyVoteChange(
            existing.SummaryRecord,
            existing.UserVote,
            record,
            userKey,
            MaxRecentCommentsPerOption);

        await PatchVotesAsync(
                session,
                modKey,
                versionKey,
                new Dictionary<string, object?>
                {
                    [$"users/{userKey}"] = record,
                    ["summary"] = updatedSummary
                },
                "Submit vote",
                cancellationToken)
            .ConfigureAwait(false);

        return await GetVoteSummaryWithEtagAsync(
                session,
                modId,
                modVersion,
                vintageStoryVersion,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(ModVersionVoteSummary Summary, string? ETag)> RemoveVoteAsync(
        string modId,
        string modVersion,
        string vintageStoryVersion,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(modId, modVersion, vintageStoryVersion);
        InternetAccessManager.ThrowIfInternetAccessDisabled();

        var session = await _authenticator
            .GetSessionAsync(cancellationToken)
            .ConfigureAwait(false);

        var modKey = SanitizeKey(modId);
        var versionKey = SanitizeKey(modVersion);
        var userKey = session.UserId ??
                      throw new InvalidOperationException("Firebase session did not provide a user ID.");

        var existing = await FetchSummaryAsync(
                session,
                modKey,
                versionKey,
                null,
                true,
                cancellationToken)
            .ConfigureAwait(false);

        var updatedSummary = ModVersionVoteAggregator.ApplyVoteChange(
            existing.SummaryRecord,
            existing.UserVote,
            null,
            userKey,
            MaxRecentCommentsPerOption);

        await PatchVotesAsync(
                session,
                modKey,
                versionKey,
                new Dictionary<string, object?>
                {
                    [$"users/{userKey}"] = null,
                    ["summary"] = updatedSummary
                },
                "Remove vote",
                cancellationToken)
            .ConfigureAwait(false);

        return await GetVoteSummaryWithEtagAsync(
                session,
                modId,
                modVersion,
                vintageStoryVersion,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(ModVersionVoteSummary Summary, string? ETag)> GetVoteSummaryWithEtagAsync(
        FirebaseAnonymousAuthenticator.FirebaseAuthSession? session,
        string modId,
        string modVersion,
        string vintageStoryVersion,
        CancellationToken cancellationToken)
    {
        var result = await GetVoteSummaryInternalAsync(
                session,
                modId,
                modVersion,
                vintageStoryVersion,
                null,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.Summary is null) throw new InvalidOperationException("Vote summary was not available.");

        return (result.Summary, result.ETag);
    }

    private async Task<VoteSummaryResult> GetVoteSummaryInternalAsync(
        FirebaseAnonymousAuthenticator.FirebaseAuthSession? session,
        string modId,
        string modVersion,
        string vintageStoryVersion,
        string? knownEtag,
        CancellationToken cancellationToken)
    {
        var modKey = SanitizeKey(modId);
        var versionKey = SanitizeKey(modVersion);
        var includeUser = session.HasValue && session.Value.UserId is { Length: > 0 };

        var summary = await FetchSummaryAsync(
                session,
                modKey,
                versionKey,
                knownEtag,
                includeUser,
                cancellationToken)
            .ConfigureAwait(false);

        if (summary.IsNotModified)
            return new VoteSummaryResult(null, summary.ETag ?? knownEtag, true);

        var domainSummary = BuildDomainSummary(
            modId,
            modVersion,
            vintageStoryVersion,
            summary.SummaryRecord,
            summary.UserVote);

        return new VoteSummaryResult(domainSummary, summary.ETag, false);
    }

    private async Task<SummaryFetchResult> FetchSummaryAsync(
        FirebaseAnonymousAuthenticator.FirebaseAuthSession? session,
        string modKey,
        string versionKey,
        string? knownEtag,
        bool includeUserVote,
        CancellationToken cancellationToken)
    {
        var url = BuildVotesUrl(session, VotesRootPath, modKey, versionKey, "summary");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("X-Firebase-ETag", "true");
        if (!string.IsNullOrWhiteSpace(knownEtag)) request.Headers.TryAddWithoutValidation("If-None-Match", knownEtag);

        using var response = await HttpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var responseEtag = response.Headers.ETag?.Tag;

        if (response.StatusCode == HttpStatusCode.NotModified)
            return new SummaryFetchResult(null, null, responseEtag ?? knownEtag, true);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return await BackfillSummaryAsync(
                    session,
                    modKey,
                    versionKey,
                    includeUserVote,
                    cancellationToken)
                .ConfigureAwait(false);

        await EnsureOkAsync(response, "Fetch summary").ConfigureAwait(false);

        await using var contentStream =
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        var summaryRecord = await JsonSerializer
            .DeserializeAsync<VoteSummaryRecord>(contentStream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        summaryRecord = NormalizeSummary(summaryRecord);

        ModVersionVoteRecord? userRecord = null;
        if (includeUserVote && session.HasValue && session.Value.UserId is { Length: > 0 } userId)
            userRecord = await FetchUserVoteAsync(session.Value, modKey, versionKey, userId, cancellationToken)
                .ConfigureAwait(false);

        return new SummaryFetchResult(summaryRecord, userRecord, responseEtag, false);
    }

    private async Task<SummaryFetchResult> BackfillSummaryAsync(
        FirebaseAnonymousAuthenticator.FirebaseAuthSession? session,
        string modKey,
        string versionKey,
        bool includeUserVote,
        CancellationToken cancellationToken)
    {
        var votes = await FetchAllVotesAsync(session, modKey, versionKey, cancellationToken).ConfigureAwait(false);

        var backfilledSummary = ModVersionVoteAggregator.CreateSummary(
            votes,
            MaxRecentCommentsPerOption,
            session?.UserId);

        if (session.HasValue)
            await WriteSummaryAsync(session, modKey, versionKey, backfilledSummary, cancellationToken)
                .ConfigureAwait(false);

        ModVersionVoteRecord? userVote = null;
        if (includeUserVote && session?.UserId is { Length: > 0 } userId)
            votes.TryGetValue(userId, out userVote);

        return new SummaryFetchResult(backfilledSummary, userVote, null, false);
    }

    private async Task<ModVersionVoteRecord?> FetchUserVoteAsync(
        FirebaseAnonymousAuthenticator.FirebaseAuthSession session,
        string modKey,
        string versionKey,
        string userKey,
        CancellationToken cancellationToken)
    {
        var url = BuildVotesUrl(session, VotesRootPath, modKey, versionKey, "users", userKey);

        using var response = await HttpClient
            .GetAsync(url, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound) return null;

        await EnsureOkAsync(response, "Fetch user vote").ConfigureAwait(false);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ModVersionVoteRecord>(payload, SerializerOptions);
    }

    private async Task<IReadOnlyDictionary<string, ModVersionVoteRecord>> FetchAllVotesAsync(
        FirebaseAnonymousAuthenticator.FirebaseAuthSession? session,
        string modKey,
        string versionKey,
        CancellationToken cancellationToken)
    {
        var url = BuildVotesUrl(session, VotesRootPath, modKey, versionKey, "users");

        using var response = await HttpClient
            .GetAsync(url, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new Dictionary<string, ModVersionVoteRecord>();

        await EnsureOkAsync(response, "Fetch votes").ConfigureAwait(false);

        await using var contentStream =
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument
            .ParseAsync(contentStream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return new Dictionary<string, ModVersionVoteRecord>();

        var result = new Dictionary<string, ModVersionVoteRecord>(StringComparer.Ordinal);

        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object) continue;

            var vote = TryDeserializeVoteRecord(property.Value);
            if (vote is null) continue;

            result[property.Name] = vote;
        }

        return result;
    }

    private static ModVersionVoteRecord? TryDeserializeVoteRecord(JsonElement element)
    {
        try
        {
            return element.Deserialize<ModVersionVoteRecord>(SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    private static VoteSummaryRecord NormalizeSummary(VoteSummaryRecord? summary)
    {
        summary ??= new VoteSummaryRecord();

        summary.RecentComments ??= new VoteSummaryComments();
        summary.RecentComments.NotFunctional ??= new List<VoteSummaryComment>();
        summary.RecentComments.CrashesOrFreezesGame ??= new List<VoteSummaryComment>();

        return summary;
    }

    private async Task WriteSummaryAsync(
        FirebaseAnonymousAuthenticator.FirebaseAuthSession? session,
        string modKey,
        string versionKey,
        VoteSummaryRecord summary,
        CancellationToken cancellationToken)
    {
        var url = BuildVotesUrl(session, VotesRootPath, modKey, versionKey, "summary");
        var payload = JsonSerializer.Serialize(summary, SerializerOptions);

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await HttpClient
            .PutAsync(url, content, cancellationToken)
            .ConfigureAwait(false);

        await EnsureOkAsync(response, "Update summary").ConfigureAwait(false);
    }

    private async Task PatchVotesAsync(
        FirebaseAnonymousAuthenticator.FirebaseAuthSession session,
        string modKey,
        string versionKey,
        IDictionary<string, object?> payload,
        string operation,
        CancellationToken cancellationToken)
    {
        var url = BuildVotesUrl(session, VotesRootPath, modKey, versionKey);
        var body = JsonSerializer.Serialize(payload, SerializerOptions);

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
        using var response = await HttpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        await EnsureOkAsync(response, operation).ConfigureAwait(false);
    }

    private static ModVersionVoteSummary BuildDomainSummary(
        string modId,
        string modVersion,
        string vintageStoryVersion,
        VoteSummaryRecord? summary,
        ModVersionVoteRecord? userVote)
    {
        if (summary is null)
        {
            return new ModVersionVoteSummary(
                modId,
                modVersion,
                NormalizeVersion(vintageStoryVersion),
                ModVersionVoteCounts.Empty,
                ModVersionVoteComments.Empty,
                null,
                null);
        }

        var normalizedSummary = NormalizeSummary(summary);

        var counts = new ModVersionVoteCounts(
            normalizedSummary.FullyFunctional,
            normalizedSummary.NoIssuesSoFar,
            normalizedSummary.SomeIssuesButWorks,
            normalizedSummary.NotFunctional,
            normalizedSummary.CrashesOrFreezesGame);

        var comments = ModVersionVoteAggregator.ToDomainComments(normalizedSummary);

        var userComment = NormalizeComment(userVote?.Comment);

        return new ModVersionVoteSummary(
            modId,
            modVersion,
            NormalizeVersion(vintageStoryVersion),
            counts,
            comments,
            userVote?.Option,
            userComment);
    }

    private string BuildVotesUrl(
        FirebaseAnonymousAuthenticator.FirebaseAuthSession? session,
        params string[] segments)
    {
        var builder = new StringBuilder();
        builder.Append(_dbUrl);

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment)) continue;

            builder.Append('/');
            builder.Append(segment);
        }

        builder.Append(".json");

        if (session.HasValue)
        {
            builder.Append("?auth=");
            builder.Append(Uri.EscapeDataString(session.Value.IdToken));
        }

        return builder.ToString();
    }

    private static async Task EnsureOkAsync(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        throw new InvalidOperationException(
            $"{operation} failed: {(int)response.StatusCode} {response.ReasonPhrase} | {Truncate(body, 200)}");
    }

    private static void ValidateIdentifiers(string modId, string modVersion, string vintageStoryVersion)
    {
        if (string.IsNullOrWhiteSpace(modId)) throw new ArgumentException("Mod identifier is required.", nameof(modId));

        if (string.IsNullOrWhiteSpace(modVersion))
            throw new ArgumentException("Mod version is required.", nameof(modVersion));

        if (string.IsNullOrWhiteSpace(vintageStoryVersion))
            throw new ArgumentException("Vintage Story version is required.", nameof(vintageStoryVersion));
    }

    private static string SanitizeKey(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return "_";

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(trimmed));
        return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string? NormalizeComment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string? NormalizeVersion(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;

        return value[..maxLength];
    }

    public readonly struct VoteSummaryResult
    {
        public VoteSummaryResult(ModVersionVoteSummary? summary, string? eTag, bool isNotModified)
        {
            Summary = summary;
            ETag = eTag;
            IsNotModified = isNotModified;
        }

        public ModVersionVoteSummary? Summary { get; }

        public string? ETag { get; }

        public bool IsNotModified { get; }
    }

    private sealed class ModVersionVoteOptionJsonConverter : JsonConverter<ModVersionVoteOption>
    {
        public override ModVersionVoteOption Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("Expected string value for mod version vote option.");

            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value)) throw new JsonException("Vote option value was empty.");

            return value switch
            {
                var v when v.Equals("fullyFunctional", StringComparison.OrdinalIgnoreCase) => ModVersionVoteOption
                    .FullyFunctional,
                var v when v.Equals("workingPerfectly", StringComparison.OrdinalIgnoreCase) => ModVersionVoteOption
                    .FullyFunctional,
                var v when v.Equals("noIssuesSoFar", StringComparison.OrdinalIgnoreCase) => ModVersionVoteOption
                    .NoIssuesSoFar,
                var v when v.Equals("someIssuesButWorks", StringComparison.OrdinalIgnoreCase) => ModVersionVoteOption
                    .SomeIssuesButWorks,
                var v when v.Equals("notFunctional", StringComparison.OrdinalIgnoreCase) => ModVersionVoteOption
                    .NotFunctional,
                var v when v.Equals("notWorking", StringComparison.OrdinalIgnoreCase) => ModVersionVoteOption
                    .NotFunctional,
                var v when v.Equals("crashesOrFreezesGame", StringComparison.OrdinalIgnoreCase) => ModVersionVoteOption
                    .CrashesOrFreezesGame,
                _ => throw new JsonException($"Unrecognized vote option '{value}'.")
            };
        }

        public override void Write(Utf8JsonWriter writer, ModVersionVoteOption value, JsonSerializerOptions options)
        {
            var stringValue = value switch
            {
                ModVersionVoteOption.FullyFunctional => "fullyFunctional",
                ModVersionVoteOption.NoIssuesSoFar => "noIssuesSoFar",
                ModVersionVoteOption.SomeIssuesButWorks => "someIssuesButWorks",
                ModVersionVoteOption.NotFunctional => "notFunctional",
                ModVersionVoteOption.CrashesOrFreezesGame => "crashesOrFreezesGame",
                _ => value.ToString() ?? string.Empty
            };

            writer.WriteStringValue(stringValue);
        }
    }

    private sealed class SummaryFetchResult
    {
        public SummaryFetchResult(
            VoteSummaryRecord? summaryRecord,
            ModVersionVoteRecord? userVote,
            string? eTag,
            bool isNotModified)
        {
            SummaryRecord = summaryRecord;
            UserVote = userVote;
            ETag = eTag;
            IsNotModified = isNotModified;
        }

        public VoteSummaryRecord? SummaryRecord { get; }

        public ModVersionVoteRecord? UserVote { get; }

        public string? ETag { get; }

        public bool IsNotModified { get; }

    }
}
