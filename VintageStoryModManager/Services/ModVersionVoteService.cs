using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using SimpleVsManager.Cloud;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
/// Provides Firebase-backed storage for per-mod user report votes.
/// </summary>
public sealed class ModVersionVoteService
{
    private const string DefaultDbUrl = "https://simple-vs-manager-default-rtdb.europe-west1.firebasedatabase.app";

    private const string VotesRootPath = "compatVotes";

    private static readonly HttpClient HttpClient = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _dbUrl;
    private readonly FirebaseAnonymousAuthenticator _authenticator;

    public ModVersionVoteService()
        : this(DefaultDbUrl, new FirebaseAnonymousAuthenticator())
    {
    }

    public ModVersionVoteService(string databaseUrl, FirebaseAnonymousAuthenticator authenticator)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            throw new ArgumentException("A Firebase database URL must be provided.", nameof(databaseUrl));
        }

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

        FirebaseAnonymousAuthenticator.FirebaseAuthSession session = await _authenticator
            .GetSessionAsync(cancellationToken)
            .ConfigureAwait(false);

        return await GetVoteSummaryInternalAsync(
                session,
                modId,
                modVersion,
                vintageStoryVersion,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ModVersionVoteSummary> SubmitVoteAsync(
        string modId,
        string modVersion,
        string vintageStoryVersion,
        ModVersionVoteOption option,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(modId, modVersion, vintageStoryVersion);
        InternetAccessManager.ThrowIfInternetAccessDisabled();

        FirebaseAnonymousAuthenticator.FirebaseAuthSession session = await _authenticator
            .GetSessionAsync(cancellationToken)
            .ConfigureAwait(false);

        string modKey = SanitizeKey(modId);
        string versionKey = SanitizeKey(modVersion);
        string userKey = session.UserId ?? throw new InvalidOperationException("Firebase session did not provide a user ID.");

        var record = new VoteRecord
        {
            Option = option,
            VintageStoryVersion = vintageStoryVersion,
            UpdatedUtc = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture)
        };

        string url = BuildAuthenticatedUrl(session.IdToken, VotesRootPath, modKey, versionKey, "users", userKey);
        string payload = JsonSerializer.Serialize(record, SerializerOptions);

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await HttpClient
            .PutAsync(url, content, cancellationToken)
            .ConfigureAwait(false);

        await EnsureOkAsync(response, "Submit vote").ConfigureAwait(false);

        return await GetVoteSummaryInternalAsync(
                session,
                modId,
                modVersion,
                vintageStoryVersion,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ModVersionVoteSummary> RemoveVoteAsync(
        string modId,
        string modVersion,
        string vintageStoryVersion,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(modId, modVersion, vintageStoryVersion);
        InternetAccessManager.ThrowIfInternetAccessDisabled();

        FirebaseAnonymousAuthenticator.FirebaseAuthSession session = await _authenticator
            .GetSessionAsync(cancellationToken)
            .ConfigureAwait(false);

        string modKey = SanitizeKey(modId);
        string versionKey = SanitizeKey(modVersion);
        string userKey = session.UserId ?? throw new InvalidOperationException("Firebase session did not provide a user ID.");

        string url = BuildAuthenticatedUrl(session.IdToken, VotesRootPath, modKey, versionKey, "users", userKey);

        using HttpResponseMessage response = await HttpClient
            .DeleteAsync(url, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureOkAsync(response, "Remove vote").ConfigureAwait(false);
        }

        return await GetVoteSummaryInternalAsync(
                session,
                modId,
                modVersion,
                vintageStoryVersion,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ModVersionVoteSummary> GetVoteSummaryInternalAsync(
        FirebaseAnonymousAuthenticator.FirebaseAuthSession session,
        string modId,
        string modVersion,
        string vintageStoryVersion,
        CancellationToken cancellationToken)
    {
        string modKey = SanitizeKey(modId);
        string versionKey = SanitizeKey(modVersion);
        string url = BuildAuthenticatedUrl(session.IdToken, VotesRootPath, modKey, versionKey, "users");

        using HttpResponseMessage response = await HttpClient
            .GetAsync(url, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ModVersionVoteSummary(
                modId,
                modVersion,
                vintageStoryVersion,
                ModVersionVoteCounts.Empty,
                null);
        }

        await EnsureOkAsync(response, "Fetch votes").ConfigureAwait(false);

        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json) || string.Equals(json.Trim(), "null", StringComparison.OrdinalIgnoreCase))
        {
            return new ModVersionVoteSummary(
                modId,
                modVersion,
                vintageStoryVersion,
                ModVersionVoteCounts.Empty,
                null);
        }

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new ModVersionVoteSummary(
                modId,
                modVersion,
                vintageStoryVersion,
                ModVersionVoteCounts.Empty,
                null);
        }

        int working = 0;
        int issues = 0;
        int failing = 0;
        ModVersionVoteOption? userVote = null;
        string? installedVersionNormalized = NormalizeVersion(vintageStoryVersion);

        foreach (JsonProperty property in root.EnumerateObject())
        {
            VoteRecord? record = TryDeserializeRecord(property.Value);
            if (record is null)
            {
                continue;
            }

            if (string.Equals(property.Name, session.UserId, StringComparison.Ordinal))
            {
                userVote = record.Value.Option;
            }

            if (!string.Equals(
                    NormalizeVersion(record.Value.VintageStoryVersion),
                    installedVersionNormalized,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            switch (record.Value.Option)
            {
                case ModVersionVoteOption.WorkingPerfectly:
                    working++;
                    break;
                case ModVersionVoteOption.SomeIssuesButWorks:
                    issues++;
                    break;
                case ModVersionVoteOption.NotWorking:
                    failing++;
                    break;
            }
        }

        var counts = new ModVersionVoteCounts(working, issues, failing);
        return new ModVersionVoteSummary(modId, modVersion, vintageStoryVersion, counts, userVote);
    }

    private static VoteRecord? TryDeserializeRecord(JsonElement element)
    {
        try
        {
            return element.Deserialize<VoteRecord>(SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    private string BuildAuthenticatedUrl(string authToken, params string[] segments)
    {
        var builder = new StringBuilder();
        builder.Append(_dbUrl);

        foreach (string segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                continue;
            }

            builder.Append('/');
            builder.Append(segment);
        }

        builder.Append(".json?auth=");
        builder.Append(Uri.EscapeDataString(authToken));
        return builder.ToString();
    }

    private static async Task EnsureOkAsync(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        throw new InvalidOperationException(
            $"{operation} failed: {(int)response.StatusCode} {response.ReasonPhrase} | {Truncate(body, 200)}");
    }

    private static void ValidateIdentifiers(string modId, string modVersion, string vintageStoryVersion)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            throw new ArgumentException("Mod identifier is required.", nameof(modId));
        }

        if (string.IsNullOrWhiteSpace(modVersion))
        {
            throw new ArgumentException("Mod version is required.", nameof(modVersion));
        }

        if (string.IsNullOrWhiteSpace(vintageStoryVersion))
        {
            throw new ArgumentException("Vintage Story version is required.", nameof(vintageStoryVersion));
        }
    }

    private static string SanitizeKey(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return "_";
        }

        string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(trimmed));
        return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string? NormalizeVersion(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : value.Trim();

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private struct VoteRecord
    {
        [JsonPropertyName("option")]
        public ModVersionVoteOption Option { get; set; }

        [JsonPropertyName("vintageStoryVersion")]
        public string? VintageStoryVersion { get; set; }

        [JsonPropertyName("updatedUtc")]
        public string? UpdatedUtc { get; set; }
    }
}
