using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
/// Provides persistence for metadata retrieved from the Vintage Story mod database so that
/// subsequent requests can be served without repeatedly downloading large payloads.
/// </summary>
internal sealed class ModDatabaseCacheService
{
    private const int CacheSchemaVersion = 1;
    private const string AnyGameVersionToken = "any";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ModDatabaseInfo?> TryLoadAsync(
        string modId,
        string? normalizedGameVersion,
        string? installedModVersion,
        CancellationToken cancellationToken)
    {
        string? cachePath = GetCacheFilePath(modId, normalizedGameVersion);
        if (string.IsNullOrWhiteSpace(cachePath) || !File.Exists(cachePath))
        {
            return null;
        }

        SemaphoreSlim fileLock = await AcquireLockAsync(cachePath, cancellationToken).ConfigureAwait(false);
        try
        {
            await using FileStream stream = new(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            CachedModDatabaseInfo? cached = await JsonSerializer
                .DeserializeAsync<CachedModDatabaseInfo>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (cached is null
                || cached.SchemaVersion != CacheSchemaVersion
                || !IsGameVersionMatch(cached.GameVersion, normalizedGameVersion))
            {
                return null;
            }

            return ConvertToDatabaseInfo(cached, normalizedGameVersion, installedModVersion);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task StoreAsync(
        string modId,
        string? normalizedGameVersion,
        ModDatabaseInfo info,
        CancellationToken cancellationToken)
    {
        if (info is null)
        {
            return;
        }

        string? cachePath = GetCacheFilePath(modId, normalizedGameVersion);
        if (string.IsNullOrWhiteSpace(cachePath))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(cachePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception)
        {
            return;
        }

        SemaphoreSlim fileLock = await AcquireLockAsync(cachePath, cancellationToken).ConfigureAwait(false);
        try
        {
            string tempPath = cachePath + ".tmp";

            CachedModDatabaseInfo cacheModel = CreateCacheModel(modId, normalizedGameVersion, info);

            await using (FileStream tempStream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(tempStream, cacheModel, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            try
            {
                File.Move(tempPath, cachePath, overwrite: true);
            }
            catch (IOException)
            {
                try
                {
                    // Retry with replace semantics when running on platforms that require it.
                    File.Replace(tempPath, cachePath, destinationBackupFileName: null);
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Ignore serialization or file system failures to keep cache best-effort.
        }
        finally
        {
            fileLock.Release();
        }
    }

    private static CachedModDatabaseInfo CreateCacheModel(
        string modId,
        string? normalizedGameVersion,
        ModDatabaseInfo info)
    {
        var releases = info.Releases ?? Array.Empty<ModReleaseInfo>();
        var releaseModels = new List<CachedModRelease>(releases.Count);

        foreach (ModReleaseInfo release in releases)
        {
            if (release?.DownloadUri is not Uri downloadUri)
            {
                continue;
            }

            releaseModels.Add(new CachedModRelease
            {
                Version = release.Version,
                NormalizedVersion = release.NormalizedVersion,
                DownloadUrl = downloadUri.ToString(),
                FileName = release.FileName,
                GameVersionTags = release.GameVersionTags?.ToArray() ?? Array.Empty<string>(),
                Changelog = release.Changelog,
                Downloads = release.Downloads,
                CreatedUtc = release.CreatedUtc
            });
        }

        return new CachedModDatabaseInfo
        {
            SchemaVersion = CacheSchemaVersion,
            CachedUtc = DateTime.UtcNow,
            ModId = modId,
            GameVersion = string.IsNullOrWhiteSpace(normalizedGameVersion)
                ? AnyGameVersionToken
                : normalizedGameVersion!,
            Tags = info.Tags?.ToArray() ?? Array.Empty<string>(),
            AssetId = info.AssetId,
            ModPageUrl = info.ModPageUrl,
            Downloads = info.Downloads,
            Comments = info.Comments,
            Follows = info.Follows,
            TrendingPoints = info.TrendingPoints,
            LogoUrl = info.LogoUrl,
            DownloadsLastThirtyDays = info.DownloadsLastThirtyDays,
            LastReleasedUtc = info.LastReleasedUtc,
            CreatedUtc = info.CreatedUtc,
            RequiredGameVersions = info.RequiredGameVersions?.ToArray() ?? Array.Empty<string>(),
            Releases = releaseModels.ToArray()
        };
    }

    private static ModDatabaseInfo? ConvertToDatabaseInfo(
        CachedModDatabaseInfo cached,
        string? normalizedGameVersion,
        string? installedModVersion)
    {
        IReadOnlyList<ModReleaseInfo> releases = BuildReleases(cached.Releases, normalizedGameVersion);

        ModReleaseInfo? latestRelease = releases.Count > 0 ? releases[0] : null;
        ModReleaseInfo? latestCompatibleRelease = releases.FirstOrDefault(r => r.IsCompatibleWithInstalledGame);

        IReadOnlyList<string> requiredVersions = DetermineRequiredGameVersions(
            cached.Releases,
            installedModVersion);

        return new ModDatabaseInfo
        {
            Tags = cached.Tags ?? Array.Empty<string>(),
            AssetId = cached.AssetId,
            ModPageUrl = cached.ModPageUrl,
            LatestCompatibleVersion = latestCompatibleRelease?.Version,
            LatestVersion = latestRelease?.Version,
            RequiredGameVersions = requiredVersions,
            Downloads = cached.Downloads,
            Comments = cached.Comments,
            Follows = cached.Follows,
            TrendingPoints = cached.TrendingPoints,
            LogoUrl = cached.LogoUrl,
            DownloadsLastThirtyDays = cached.DownloadsLastThirtyDays,
            LastReleasedUtc = cached.LastReleasedUtc,
            CreatedUtc = cached.CreatedUtc,
            LatestRelease = latestRelease,
            LatestCompatibleRelease = latestCompatibleRelease,
            Releases = releases
        };
    }

    private static IReadOnlyList<ModReleaseInfo> BuildReleases(
        IReadOnlyList<CachedModRelease>? cachedReleases,
        string? normalizedGameVersion)
    {
        if (cachedReleases is null || cachedReleases.Count == 0)
        {
            return Array.Empty<ModReleaseInfo>();
        }

        var releases = new List<ModReleaseInfo>(cachedReleases.Count);

        foreach (CachedModRelease release in cachedReleases)
        {
            if (string.IsNullOrWhiteSpace(release.Version)
                || string.IsNullOrWhiteSpace(release.DownloadUrl)
                || !Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out Uri? downloadUri))
            {
                continue;
            }

            bool isCompatible = false;
            if (normalizedGameVersion != null && release.GameVersionTags is { Length: > 0 })
            {
                foreach (string tag in release.GameVersionTags)
                {
                    if (VersionStringUtility.MatchesVersionOrPrefix(tag, normalizedGameVersion))
                    {
                        isCompatible = true;
                        break;
                    }
                }
            }

            releases.Add(new ModReleaseInfo
            {
                Version = release.Version,
                NormalizedVersion = release.NormalizedVersion,
                DownloadUri = downloadUri,
                FileName = release.FileName,
                GameVersionTags = release.GameVersionTags ?? Array.Empty<string>(),
                IsCompatibleWithInstalledGame = isCompatible,
                Changelog = release.Changelog,
                Downloads = release.Downloads,
                CreatedUtc = release.CreatedUtc
            });
        }

        return releases.Count == 0 ? Array.Empty<ModReleaseInfo>() : releases;
    }

    private static IReadOnlyList<string> DetermineRequiredGameVersions(
        IReadOnlyList<CachedModRelease>? cachedReleases,
        string? installedModVersion)
    {
        if (string.IsNullOrWhiteSpace(installedModVersion)
            || cachedReleases is null
            || cachedReleases.Count == 0)
        {
            return Array.Empty<string>();
        }

        string? normalizedInstalledVersion = VersionStringUtility.Normalize(installedModVersion);

        foreach (CachedModRelease release in cachedReleases)
        {
            if (string.IsNullOrWhiteSpace(release.Version))
            {
                continue;
            }

            if (ReleaseMatchesInstalledVersion(
                    release.Version,
                    release.NormalizedVersion,
                    installedModVersion,
                    normalizedInstalledVersion))
            {
                string[] tags = release.GameVersionTags ?? Array.Empty<string>();
                return tags.Length == 0 ? Array.Empty<string>() : tags;
            }
        }

        return Array.Empty<string>();
    }

    private static bool ReleaseMatchesInstalledVersion(
        string releaseVersion,
        string? normalizedReleaseVersion,
        string installedVersion,
        string? normalizedInstalledVersion)
    {
        if (string.Equals(releaseVersion, installedVersion, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(normalizedReleaseVersion) || string.IsNullOrWhiteSpace(normalizedInstalledVersion))
        {
            return false;
        }

        return string.Equals(normalizedReleaseVersion, normalizedInstalledVersion, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<SemaphoreSlim> AcquireLockAsync(string path, CancellationToken cancellationToken)
    {
        SemaphoreSlim gate = _fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return gate;
    }

    private static string? GetCacheFilePath(string modId, string? normalizedGameVersion)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return null;
        }

        string? baseDirectory = ModCacheLocator.GetModDatabaseCacheDirectory();
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return null;
        }

        string safeModId = ModCacheLocator.SanitizeFileName(modId, "mod");
        string safeGameVersion = string.IsNullOrWhiteSpace(normalizedGameVersion)
            ? AnyGameVersionToken
            : ModCacheLocator.SanitizeFileName(normalizedGameVersion!, "game");

        string fileName = $"{safeModId}__{safeGameVersion}.json";

        return Path.Combine(baseDirectory, fileName);
    }

    private static bool IsGameVersionMatch(string? cachedGameVersion, string? normalizedGameVersion)
    {
        string cachedValue = string.IsNullOrWhiteSpace(cachedGameVersion) ? AnyGameVersionToken : cachedGameVersion;
        string currentValue = string.IsNullOrWhiteSpace(normalizedGameVersion) ? AnyGameVersionToken : normalizedGameVersion;
        return string.Equals(cachedValue, currentValue, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CachedModDatabaseInfo
    {
        public int SchemaVersion { get; init; }

        public string ModId { get; init; } = string.Empty;

        public string GameVersion { get; init; } = AnyGameVersionToken;

        public DateTime CachedUtc { get; init; }

        public string[] Tags { get; init; } = Array.Empty<string>();

        public string? AssetId { get; init; }

        public string? ModPageUrl { get; init; }

        public int? Downloads { get; init; }

        public int? Comments { get; init; }

        public int? Follows { get; init; }

        public int? TrendingPoints { get; init; }

        public string? LogoUrl { get; init; }

        public int? DownloadsLastThirtyDays { get; init; }

        public DateTime? LastReleasedUtc { get; init; }

        public DateTime? CreatedUtc { get; init; }

        public string[] RequiredGameVersions { get; init; } = Array.Empty<string>();

        public CachedModRelease[] Releases { get; init; } = Array.Empty<CachedModRelease>();
    }

    private sealed class CachedModRelease
    {
        public string Version { get; init; } = string.Empty;

        public string? NormalizedVersion { get; init; }

        public string DownloadUrl { get; init; } = string.Empty;

        public string? FileName { get; init; }

        public string[] GameVersionTags { get; init; } = Array.Empty<string>();

        public string? Changelog { get; init; }

        public int? Downloads { get; init; }

        public DateTime? CreatedUtc { get; init; }
    }
}
