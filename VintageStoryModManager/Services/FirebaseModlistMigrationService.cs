using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using SimpleVsManager.Cloud;

namespace VintageStoryModManager.Services;

internal sealed class FirebaseModlistMigrationService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<FirebaseModlistMigrationResult?> TryMigrateAsync(
        string? playerUid,
        string? playerName,
        CancellationToken ct,
        IProgress<FirebaseMigrationProgress>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(playerUid)) return null;

        var stateFilePath = FirebaseAnonymousAuthenticator.GetStateFilePath();
        if (string.IsNullOrWhiteSpace(stateFilePath) || !File.Exists(stateFilePath)) return null;

        var projectId = TryReadProjectId(stateFilePath);
        if (IsNewProject(projectId) || !IsLegacyProject(projectId)) return null;

        progress?.Report(new FirebaseMigrationProgress("Starting cloud modlist migration..."));
        StatusLogService.AppendStatus("Migrating cloud modlists to the new Firebase project...", false);

        var legacyAuthenticator = new FirebaseAnonymousAuthenticator(DevConfig.FirebaseLegacyApiKey);
        var legacyStore = new FirebaseModlistStore(DevConfig.FirebaseLegacyModlistDbUrl, legacyAuthenticator);
        legacyStore.SetPlayerIdentity(playerUid, playerName);

        progress?.Report(new FirebaseMigrationProgress("Checking for legacy cloud modlists..."));
        var modlists = await DownloadLegacyModlistsAsync(legacyStore, ct, progress).ConfigureAwait(false);

        var backupPath = TryBackupLegacyAuthFile(stateFilePath);

        var legacySession = await legacyStore.Authenticator.TryGetExistingSessionAsync(ct).ConfigureAwait(false)
                            ?? await legacyStore.Authenticator.GetSessionAsync(ct).ConfigureAwait(false);

        var newStore = new FirebaseModlistStore();
        newStore.SetPlayerIdentity(playerUid, playerName);

        progress?.Report(new FirebaseMigrationProgress("Preparing new Firebase session..."));
        var newSession = await newStore.Authenticator.GetSessionAsync(ct).ConfigureAwait(false);

        foreach (var modlist in modlists)
        {
            progress?.Report(new FirebaseMigrationProgress(
                $"Saving \"{modlist.DisplayName ?? modlist.SlotKey}\" to the new project...",
                modlist.DisplayName ?? modlist.SlotKey));
            await newStore.SaveAsync(modlist.SlotKey, modlist.ContentJson, ct).ConfigureAwait(false);
        }

        StatusLogService.AppendStatus(
            "Cloud modlists migrated to the new Firebase project successfully." +
            (string.IsNullOrWhiteSpace(backupPath) ? string.Empty : $" Legacy auth saved to {backupPath} for reference."),
            false);

        return new FirebaseModlistMigrationResult(true, legacySession.UserId, newSession.UserId, modlists);
    }

    private static bool IsNewProject(string? projectId)
    {
        return string.Equals(projectId, DevConfig.FirebaseNewProjectId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegacyProject(string? projectId)
    {
        return string.Equals(projectId, DevConfig.FirebaseLegacyProjectId, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<LegacyModlist>> DownloadLegacyModlistsAsync(
        FirebaseModlistStore legacyStore,
        CancellationToken ct,
        IProgress<FirebaseMigrationProgress>? progress)
    {
        var result = new List<LegacyModlist>();
        var slots = await legacyStore.ListSlotsAsync(ct).ConfigureAwait(false);

        foreach (var slot in slots)
        {
            try
            {
                progress?.Report(new FirebaseMigrationProgress($"Downloading modlist from {slot}...", slot));
                var content = await legacyStore.LoadAsync(slot, ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(content)) continue;

                var legacyModlist = new LegacyModlist(slot, content, TryExtractModlistName(content));
                result.Add(legacyModlist);
                progress?.Report(new FirebaseMigrationProgress(
                    $"Finished downloading \"{legacyModlist.DisplayName ?? slot}\".",
                    legacyModlist.DisplayName ?? slot));
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
            {
                StatusLogService.AppendStatus($"Failed to read legacy modlist for {slot}: {ex.Message}", true);
            }
        }

        return result;
    }

    private string? TryBackupLegacyAuthFile(string stateFilePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(stateFilePath);
            var fileName = Path.GetFileName(stateFilePath);

            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
                return null;

            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            var backupName = $"{fileName}.legacy.{timestamp}";
            var backupPath = Path.Combine(directory, backupName);

            File.Move(stateFilePath, backupPath, true);

            return backupPath;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (SecurityException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private string? TryReadProjectId(string stateFilePath)
    {
        try
        {
            var json = File.ReadAllText(stateFilePath);
            if (string.IsNullOrWhiteSpace(json)) return null;

            var model = JsonSerializer.Deserialize<AuthStateModel>(json, _jsonOptions);
            return TryExtractProjectId(model?.IdToken);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (SecurityException)
        {
            return null;
        }
    }

    private static string? TryExtractProjectId(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken)) return null;

        var parts = idToken.Split('.');
        if (parts.Length < 2) return null;

        try
        {
            var payload = parts[1];
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            payload = payload.Replace('-', '+').Replace('_', '/');

            var bytes = Convert.FromBase64String(payload);
            using var document = JsonDocument.Parse(bytes);

            if (document.RootElement.TryGetProperty("iss", out var issElement)
                && issElement.ValueKind == JsonValueKind.String)
            {
                var issuer = issElement.GetString();
                if (!string.IsNullOrWhiteSpace(issuer))
                {
                    var idx = issuer!.LastIndexOf("/", StringComparison.Ordinal);
                    return idx >= 0 && idx < issuer.Length - 1 ? issuer[(idx + 1)..] : issuer;
                }
            }

            if (document.RootElement.TryGetProperty("aud", out var audElement)
                && audElement.ValueKind == JsonValueKind.String)
                return audElement.GetString();
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? TryExtractModlistName(string? contentJson)
    {
        if (string.IsNullOrWhiteSpace(contentJson)) return null;

        try
        {
            using var document = JsonDocument.Parse(contentJson);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("name", out var nameProperty) && nameProperty.ValueKind == JsonValueKind.String)
                {
                    var name = nameProperty.GetString();
                    if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
                }

                if (root.TryGetProperty("content", out var contentProperty) && contentProperty.ValueKind == JsonValueKind.Object
                    && contentProperty.TryGetProperty("name", out var contentName)
                    && contentName.ValueKind == JsonValueKind.String)
                {
                    var name = contentName.GetString();
                    if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    public sealed record FirebaseMigrationProgress(string Message, string? ModlistName = null)
    {
        public string Message { get; } = string.IsNullOrWhiteSpace(Message)
            ? throw new ArgumentException("A progress message is required.", nameof(Message))
            : Message.Trim();

        public string? ModlistName { get; } = string.IsNullOrWhiteSpace(ModlistName) ? null : ModlistName.Trim();
    }

    public sealed record FirebaseModlistMigrationResult(
        bool Success,
        string? LegacyUserId,
        string? NewUserId,
        IReadOnlyList<LegacyModlist> MigratedModlists);

    public sealed record LegacyModlist(string SlotKey, string ContentJson, string? DisplayName);

    private sealed class AuthStateModel
    {
        public string? IdToken { get; set; }
        public string? UserId { get; set; }
    }
}
