using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
///     Provides persistence for metadata retrieved from the Vintage Story mod database so that
///     subsequent requests can be served without repeatedly downloading large payloads.
///     All cache entries are stored in a single file for improved IO performance.
/// </summary>
internal sealed class ModDatabaseCacheService : IDisposable
{
    private static readonly int CacheSchemaVersion = DevConfig.ModDatabaseCacheSchemaVersion;

    private static readonly int MinimumSupportedCacheSchemaVersion =
        DevConfig.ModDatabaseMinimumSupportedCacheSchemaVersion;

    private static readonly string AnyGameVersionToken = DevConfig.ModDatabaseAnyGameVersionToken;
    private static readonly TimeSpan CacheEntryMaxAge = TimeSpan.FromHours(12);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private Dictionary<string, CachedModDatabaseInfo>? _cacheIndex;
    private bool _isDirty;
    private bool _disposed;

    internal static void ClearCacheDirectory()
    {
        var cacheFilePath = GetCacheFilePath();
        if (string.IsNullOrWhiteSpace(cacheFilePath)) return;

        try
        {
            if (File.Exists(cacheFilePath))
                File.Delete(cacheFilePath);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to delete the mod database cache at {cacheFilePath}.", ex);
        }
    }

    public async Task<ModDatabaseInfo?> TryLoadAsync(
        string modId,
        string? normalizedGameVersion,
        string? installedModVersion,
        bool allowExpiredEntryRefresh,
        bool requireExactVersionMatch,
        CancellationToken cancellationToken)
    {
        var cacheKey = GetCacheKey(modId, normalizedGameVersion);
        
        await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await EnsureCacheLoadedAsync(cancellationToken).ConfigureAwait(false);
            
            if (!index.TryGetValue(cacheKey, out var cached)) return null;

            if (!IsSupportedSchemaVersion(cached.SchemaVersion)
                || !IsGameVersionMatch(cached.GameVersion, normalizedGameVersion))
                return null;

            if (IsCacheEntryExpired(cached.CachedUtc))
            {
                var canRefreshExpiredEntry = allowExpiredEntryRefresh
                                             && !InternetAccessManager.IsInternetAccessDisabled;

                if (!canRefreshExpiredEntry)
                    return ConvertToDatabaseInfo(cached, normalizedGameVersion, installedModVersion,
                        requireExactVersionMatch);

                return null;
            }

            return ConvertToDatabaseInfo(cached, normalizedGameVersion, installedModVersion, requireExactVersionMatch);
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
            _cacheLock.Release();
        }
    }

    public async Task StoreAsync(
        string modId,
        string? normalizedGameVersion,
        ModDatabaseInfo info,
        string? installedModVersion,
        CancellationToken cancellationToken)
    {
        if (info is null) return;

        var cacheKey = GetCacheKey(modId, normalizedGameVersion);

        await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await EnsureCacheLoadedAsync(cancellationToken).ConfigureAwait(false);

            // Load existing entry to preserve tags by version
            var tagsByModVersion = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            if (index.TryGetValue(cacheKey, out var existingEntry) && existingEntry.TagsByModVersion is { Count: > 0 })
            {
                tagsByModVersion = new Dictionary<string, string[]>(existingEntry.TagsByModVersion, StringComparer.OrdinalIgnoreCase);
            }

            var tagsVersionKey = NormalizeModVersion(info.CachedTagsVersion);
            if (string.IsNullOrWhiteSpace(tagsVersionKey)) tagsVersionKey = NormalizeModVersion(installedModVersion);

            if (!string.IsNullOrWhiteSpace(tagsVersionKey))
                tagsByModVersion[tagsVersionKey] = info.Tags?.ToArray() ?? Array.Empty<string>();

            var cacheModel = CreateCacheModel(modId, normalizedGameVersion, info, tagsByModVersion);
            index[cacheKey] = cacheModel;
            
            // Set dirty flag after successful cache update
            _isDirty = true;

            // Do not write to disk immediately during bulk operations
            // Caller should invoke FlushAsync when appropriate
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
            _cacheLock.Release();
        }
    }

    /// <summary>
    ///     Pre-loads the cache into memory if not already loaded.
    ///     This should be called before bulk operations to ensure the cache is ready.
    /// </summary>
    public async Task PreloadCacheAsync(CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureCacheLoadedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Ignore cache load failures - lazy loading will handle it later if needed.
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    ///     Flushes any pending cache changes to disk.
    ///     Should be called after bulk operations to persist accumulated updates.
    /// </summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_isDirty || _cacheIndex == null) return;

            await SaveCacheAsync(_cacheIndex, cancellationToken).ConfigureAwait(false);
            _isDirty = false;
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
            _cacheLock.Release();
        }
    }

    private async Task<Dictionary<string, CachedModDatabaseInfo>> EnsureCacheLoadedAsync(CancellationToken cancellationToken)
    {
        if (_cacheIndex != null) return _cacheIndex;

        _cacheIndex = await LoadCacheAsync(cancellationToken).ConfigureAwait(false);
        return _cacheIndex;
    }

    private async Task<Dictionary<string, CachedModDatabaseInfo>> LoadCacheAsync(CancellationToken cancellationToken)
    {
        var cacheFilePath = GetCacheFilePath();
        if (string.IsNullOrWhiteSpace(cacheFilePath) || !File.Exists(cacheFilePath))
            return new Dictionary<string, CachedModDatabaseInfo>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await using FileStream stream = new(cacheFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var cache = await JsonSerializer
                .DeserializeAsync<ModDatabaseCache>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (cache?.Entries == null)
                return new Dictionary<string, CachedModDatabaseInfo>(StringComparer.OrdinalIgnoreCase);

            return cache.Entries;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new Dictionary<string, CachedModDatabaseInfo>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SaveCacheAsync(Dictionary<string, CachedModDatabaseInfo> index, CancellationToken cancellationToken)
    {
        var cacheFilePath = GetCacheFilePath();
        if (string.IsNullOrWhiteSpace(cacheFilePath)) return;

        var directory = Path.GetDirectoryName(cacheFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var tempPath = cacheFilePath + ".tmp";

        var cache = new ModDatabaseCache
        {
            SchemaVersion = CacheSchemaVersion,
            Entries = index
        };

        await using (FileStream tempStream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(tempStream, cache, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        MoveCacheFile(tempPath, cacheFilePath);
    }

    private void SaveCacheSync(Dictionary<string, CachedModDatabaseInfo> index)
    {
        var cacheFilePath = GetCacheFilePath();
        if (string.IsNullOrWhiteSpace(cacheFilePath)) return;

        var directory = Path.GetDirectoryName(cacheFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var tempPath = cacheFilePath + ".tmp";

        var cache = new ModDatabaseCache
        {
            SchemaVersion = CacheSchemaVersion,
            Entries = index
        };

        using (FileStream tempStream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(tempStream, cache, SerializerOptions);
        }

        MoveCacheFile(tempPath, cacheFilePath);
    }

    private static void MoveCacheFile(string tempPath, string cacheFilePath)
    {
        try
        {
            File.Move(tempPath, cacheFilePath, true);
        }
        catch (IOException)
        {
            try
            {
                // Retry with replace semantics when running on platforms that require it.
                File.Replace(tempPath, cacheFilePath, null);
            }
            catch
            {
                // Clean up temp file only if replace also failed
                if (File.Exists(tempPath)) File.Delete(tempPath);
                throw;
            }
        }
    }

    private static string GetCacheKey(string modId, string? normalizedGameVersion)
    {
        if (string.IsNullOrWhiteSpace(modId))
            throw new ArgumentException("Mod ID cannot be null or empty.", nameof(modId));

        var safeGameVersion = string.IsNullOrWhiteSpace(normalizedGameVersion)
            ? AnyGameVersionToken
            : normalizedGameVersion;

        return $"{modId}__{safeGameVersion}";
    }

    private static string? NormalizeModVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;

        return VersionStringUtility.Normalize(version);
    }

    private static CachedModDatabaseInfo CreateCacheModel(
        string modId,
        string? normalizedGameVersion,
        ModDatabaseInfo info,
        IReadOnlyDictionary<string, string[]> tagsByModVersion)
    {
        var releases = info.Releases ?? Array.Empty<ModReleaseInfo>();
        var releaseModels = new List<CachedModRelease>(releases.Count);

        foreach (var release in releases)
        {
            if (release?.DownloadUri is not Uri downloadUri) continue;

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
            TagsByModVersion = tagsByModVersion.Count == 0
                ? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string[]>(tagsByModVersion, StringComparer.OrdinalIgnoreCase),
            AssetId = info.AssetId,
            ModPageUrl = info.ModPageUrl,
            Downloads = info.Downloads,
            Comments = info.Comments,
            Follows = info.Follows,
            TrendingPoints = info.TrendingPoints,
            LogoUrl = info.LogoUrl,
            DownloadsLastThirtyDays = info.DownloadsLastThirtyDays,
            DownloadsLastTenDays = info.DownloadsLastTenDays,
            LastReleasedUtc = info.LastReleasedUtc,
            CreatedUtc = info.CreatedUtc,
            RequiredGameVersions = info.RequiredGameVersions?.ToArray() ?? Array.Empty<string>(),
            Releases = releaseModels.ToArray(),
            Side = info.Side
        };
    }

    private static ModDatabaseInfo? ConvertToDatabaseInfo(
        CachedModDatabaseInfo cached,
        string? normalizedGameVersion,
        string? installedModVersion,
        bool requireExactVersionMatch)
    {
        var normalizedInstalledVersion = NormalizeModVersion(installedModVersion);

        var tags = GetTagsForInstalledVersion(
            cached,
            normalizedInstalledVersion,
            out var cachedTagsVersion);

        var releases = BuildReleases(cached.Releases, normalizedGameVersion, requireExactVersionMatch);

        var latestRelease = releases.Count > 0 ? releases[0] : null;
        var latestCompatibleRelease = releases.FirstOrDefault(r => r.IsCompatibleWithInstalledGame);

        var requiredVersions = DetermineRequiredGameVersions(
            cached.Releases,
            installedModVersion);

        return new ModDatabaseInfo
        {
            Tags = tags,
            CachedTagsVersion = cachedTagsVersion,
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
            DownloadsLastTenDays = cached.DownloadsLastTenDays,
            LastReleasedUtc = cached.LastReleasedUtc,
            CreatedUtc = cached.CreatedUtc,
            LatestRelease = latestRelease,
            LatestCompatibleRelease = latestCompatibleRelease,
            Releases = releases,
            Side = cached.Side
        };
    }

    private static IReadOnlyList<string> GetTagsForInstalledVersion(
        CachedModDatabaseInfo cached,
        string? normalizedInstalledVersion,
        out string? cachedTagsVersion)
    {
        cachedTagsVersion = null;

        if (!string.IsNullOrWhiteSpace(normalizedInstalledVersion)
            && cached.TagsByModVersion is { Count: > 0 }
            && cached.TagsByModVersion.TryGetValue(normalizedInstalledVersion, out var versionTags))
        {
            cachedTagsVersion = normalizedInstalledVersion;
            return versionTags is { Length: > 0 } ? versionTags : Array.Empty<string>();
        }

        if (cached.Tags is { Length: > 0 }) return cached.Tags;

        if (cached.TagsByModVersion is { Count: > 0 })
            foreach (var entry in cached.TagsByModVersion)
                if (entry.Value is { Length: > 0 })
                    return entry.Value;

        return Array.Empty<string>();
    }

    private static IReadOnlyList<ModReleaseInfo> BuildReleases(
        IReadOnlyList<CachedModRelease>? cachedReleases,
        string? normalizedGameVersion,
        bool requireExactVersionMatch)
    {
        if (cachedReleases is null || cachedReleases.Count == 0) return Array.Empty<ModReleaseInfo>();

        var releases = new List<ModReleaseInfo>(cachedReleases.Count);

        foreach (var release in cachedReleases)
        {
            if (string.IsNullOrWhiteSpace(release.Version)
                || string.IsNullOrWhiteSpace(release.DownloadUrl)
                || !Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out var downloadUri))
                continue;

            var isCompatible = false;
            if (normalizedGameVersion != null && release.GameVersionTags is { Length: > 0 })
                foreach (var tag in release.GameVersionTags)
                    if (VersionStringUtility.SupportsVersion(tag, normalizedGameVersion, requireExactVersionMatch))
                    {
                        isCompatible = true;
                        break;
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
            return Array.Empty<string>();

        var normalizedInstalledVersion = VersionStringUtility.Normalize(installedModVersion);

        foreach (var release in cachedReleases)
        {
            if (string.IsNullOrWhiteSpace(release.Version)) continue;

            if (ReleaseMatchesInstalledVersion(
                    release.Version,
                    release.NormalizedVersion,
                    installedModVersion,
                    normalizedInstalledVersion))
            {
                var tags = release.GameVersionTags ?? Array.Empty<string>();
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
        if (string.Equals(releaseVersion, installedVersion, StringComparison.OrdinalIgnoreCase)) return true;

        if (string.IsNullOrWhiteSpace(normalizedReleaseVersion) ||
            string.IsNullOrWhiteSpace(normalizedInstalledVersion)) return false;

        return string.Equals(normalizedReleaseVersion, normalizedInstalledVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetCacheFilePath()
    {
        var baseDirectory = ModCacheLocator.GetModDatabaseCacheDirectory();
        if (string.IsNullOrWhiteSpace(baseDirectory)) return null;

        return Path.Combine(baseDirectory, "mod-database-cache.json");
    }

    private static bool IsCacheEntryExpired(DateTime cachedUtc)
    {
        if (cachedUtc == default) return true;

        var expirationThreshold = DateTime.UtcNow - CacheEntryMaxAge;
        return cachedUtc < expirationThreshold;
    }

    private static bool IsSupportedSchemaVersion(int schemaVersion)
    {
        return schemaVersion >= MinimumSupportedCacheSchemaVersion
               && schemaVersion <= CacheSchemaVersion;
    }

    private static bool IsGameVersionMatch(string? cachedGameVersion, string? normalizedGameVersion)
    {
        var cachedValue = string.IsNullOrWhiteSpace(cachedGameVersion) ? AnyGameVersionToken : cachedGameVersion;
        var currentValue = string.IsNullOrWhiteSpace(normalizedGameVersion)
            ? AnyGameVersionToken
            : normalizedGameVersion;
        return string.Equals(cachedValue, currentValue, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        // Attempt to flush any pending changes before disposal
        // Try to acquire lock with zero timeout to avoid blocking during disposal
        if (_cacheLock.Wait(0))
        {
            try
            {
                // Check if there are pending changes to flush
                if (_isDirty && _cacheIndex != null)
                {
                    // Use synchronous save for disposal because Dispose cannot use async/await
                    // (IDisposable.Dispose is a synchronous method)
                    SaveCacheSync(_cacheIndex);
                    _isDirty = false;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Best effort - ignore expected cache write errors during disposal
            }
            finally
            {
                _cacheLock.Release();
            }
        }
        // If we couldn't acquire the lock, skip the flush (best effort)
        
        _cacheLock.Dispose();
    }

    private sealed class ModDatabaseCache
    {
        public int SchemaVersion { get; init; }

        public Dictionary<string, CachedModDatabaseInfo> Entries { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class CachedModDatabaseInfo
    {
        public int SchemaVersion { get; init; }

        public string ModId { get; init; } = string.Empty;

        public string GameVersion { get; init; } = AnyGameVersionToken;

        public DateTime CachedUtc { get; init; }

        public string[] Tags { get; init; } = Array.Empty<string>();

        public Dictionary<string, string[]> TagsByModVersion { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public string? AssetId { get; init; }

        public string? ModPageUrl { get; init; }

        public int? Downloads { get; init; }

        public int? Comments { get; init; }

        public int? Follows { get; init; }

        public int? TrendingPoints { get; init; }

        public string? LogoUrl { get; init; }

        public int? DownloadsLastThirtyDays { get; init; }

        public int? DownloadsLastTenDays { get; init; }

        public DateTime? LastReleasedUtc { get; init; }

        public DateTime? CreatedUtc { get; init; }

        public string[] RequiredGameVersions { get; init; } = Array.Empty<string>();

        public CachedModRelease[] Releases { get; init; } = Array.Empty<CachedModRelease>();

        public string? Side { get; init; }
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