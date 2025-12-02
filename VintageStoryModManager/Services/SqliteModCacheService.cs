using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
///     Provides unified SQLite-backed caching for mod manifests and database info.
///     Replaces the separate JSON-based ModManifestCacheService and ModDatabaseCacheService.
///     Images are stored in Temp Cache/Images/ with filenames stored in the database.
/// </summary>
internal sealed class SqliteModCacheService : IDisposable
{
    private const int SchemaVersion = 1;
    private const string DatabaseFileName = "mod-cache.db";
    
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private SqliteConnection? _connection;
    private readonly object _connectionLock = new();
    private bool _initialized;
    
    /// <summary>
    /// Static HttpClient instance for reuse across all image downloads.
    /// HttpClient is designed to be long-lived and reused, not disposed per request.
    /// </summary>
    private static readonly System.Net.Http.HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    /// <summary>
    ///     Initializes the database connection and schema.
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;

        var dbPath = GetDatabasePath();
        if (string.IsNullOrWhiteSpace(dbPath)) return;

        // Validate path to prevent path injection
        try
        {
            dbPath = Path.GetFullPath(dbPath);
        }
        catch (Exception)
        {
            return;
        }

        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        lock (_connectionLock)
        {
            if (_initialized) return;

            _connection = new SqliteConnection($"Data Source={dbPath}");
            _connection.Open();

            // Enable WAL mode for better concurrent access
            using var walCommand = _connection.CreateCommand();
            walCommand.CommandText = "PRAGMA journal_mode=WAL;";
            walCommand.ExecuteNonQuery();

            CreateSchema();
            _initialized = true;
        }
    }

    private void CreateSchema()
    {
        if (_connection is null) return;

        using var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS schema_version (
                version INTEGER PRIMARY KEY
            );

            -- Manifest cache: stores data extracted from mod files, keyed by source path
            CREATE TABLE IF NOT EXISTS manifest_cache (
                source_path TEXT PRIMARY KEY,
                mod_id TEXT NOT NULL,
                version TEXT NOT NULL,
                file_length INTEGER NOT NULL,
                last_write_time_utc_ticks INTEGER NOT NULL,
                manifest_json TEXT NOT NULL,
                icon_filename TEXT,
                created_at TEXT NOT NULL
            );

            -- Database cache: stores data from mod database API, keyed by mod_id + game_version
            CREATE TABLE IF NOT EXISTS database_cache (
                mod_id TEXT NOT NULL,
                game_version TEXT NOT NULL,
                tags_json TEXT,
                asset_id TEXT,
                mod_page_url TEXT,
                latest_compatible_version TEXT,
                latest_version TEXT,
                required_game_versions_json TEXT,
                downloads INTEGER,
                comments INTEGER,
                follows INTEGER,
                trending_points INTEGER,
                logo_filename TEXT,
                downloads_last_30_days INTEGER,
                downloads_last_10_days INTEGER,
                last_released_utc TEXT,
                created_utc TEXT,
                releases_json TEXT,
                side TEXT,
                last_modified_header TEXT,
                etag TEXT,
                last_modified_api_value TEXT,
                cached_at TEXT NOT NULL,
                tags_by_version_json TEXT,
                PRIMARY KEY (mod_id, game_version)
            );

            CREATE INDEX IF NOT EXISTS idx_manifest_mod_id ON manifest_cache(mod_id);
            CREATE INDEX IF NOT EXISTS idx_manifest_mod_version ON manifest_cache(mod_id, version);
            CREATE INDEX IF NOT EXISTS idx_database_mod_id ON database_cache(mod_id);
        ";
        command.ExecuteNonQuery();

        // Check and set schema version
        using var versionCommand = _connection.CreateCommand();
        versionCommand.CommandText = "SELECT version FROM schema_version LIMIT 1";
        var existingVersion = versionCommand.ExecuteScalar() as long?;

        if (!existingVersion.HasValue)
        {
            using var insertCommand = _connection.CreateCommand();
            insertCommand.CommandText = "INSERT INTO schema_version (version) VALUES ($version)";
            insertCommand.Parameters.AddWithValue("$version", SchemaVersion);
            insertCommand.ExecuteNonQuery();
        }
    }

    #region Manifest Cache Methods

    /// <summary>
    ///     Attempts to retrieve cached manifest data for a mod file.
    /// </summary>
    public bool TryGetManifest(
        string sourcePath,
        DateTime lastWriteTimeUtc,
        long length,
        out string manifestJson,
        out byte[]? iconBytes)
    {
        manifestJson = string.Empty;
        iconBytes = null;

        if (!_initialized || _connection is null) return false;

        var normalizedPath = NormalizePath(sourcePath);
        var ticks = ToUniversalTicks(lastWriteTimeUtc);

        lock (_connectionLock)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT manifest_json, icon_filename, file_length, last_write_time_utc_ticks
                FROM manifest_cache
                WHERE source_path = $path
                LIMIT 1";
            command.Parameters.AddWithValue("$path", normalizedPath);

            using var reader = command.ExecuteReader();
            if (!reader.Read()) return false;

            var cachedLength = reader.GetInt64(2);
            var cachedTicks = reader.GetInt64(3);

            // Invalidate if file changed
            if (cachedLength != length || cachedTicks != ticks)
            {
                return false;
            }

            manifestJson = reader.GetString(0);
            
            var iconFilename = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (!string.IsNullOrWhiteSpace(iconFilename))
            {
                var iconPath = GetImagePath(iconFilename);
                if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
                {
                    try
                    {
                        iconBytes = File.ReadAllBytes(iconPath);
                    }
                    catch (Exception)
                    {
                        // Icon read failed, but manifest is still valid
                    }
                }
            }

            return true;
        }
    }

    /// <summary>
    ///     Stores manifest data for a mod file.
    /// </summary>
    public void StoreManifest(
        string sourcePath,
        DateTime lastWriteTimeUtc,
        long length,
        string modId,
        string? version,
        string manifestJson,
        byte[]? iconBytes)
    {
        if (!_initialized || _connection is null) return;
        if (string.IsNullOrWhiteSpace(modId)) return;

        var normalizedPath = NormalizePath(sourcePath);
        var ticks = ToUniversalTicks(lastWriteTimeUtc);
        var normalizedVersion = GetVersionOrDefault(version);

        string? iconFilename = null;
        if (iconBytes is { Length: > 0 })
        {
            iconFilename = $"{SanitizeForFilename(modId)}_{SanitizeForFilename(normalizedVersion)}_icon.png";
            var iconPath = GetImagePath(iconFilename);
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                try
                {
                    var directory = Path.GetDirectoryName(iconPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    File.WriteAllBytes(iconPath, iconBytes);
                }
                catch (Exception)
                {
                    // Icon storage failed, but we can still cache manifest
                    iconFilename = null;
                }
            }
        }

        lock (_connectionLock)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO manifest_cache 
                (source_path, mod_id, version, file_length, last_write_time_utc_ticks, 
                 manifest_json, icon_filename, created_at)
                VALUES 
                ($path, $modId, $version, $length, $ticks, $manifest, $icon, $createdAt)";
            
            command.Parameters.AddWithValue("$path", normalizedPath);
            command.Parameters.AddWithValue("$modId", modId);
            command.Parameters.AddWithValue("$version", normalizedVersion);
            command.Parameters.AddWithValue("$length", length);
            command.Parameters.AddWithValue("$ticks", ticks);
            command.Parameters.AddWithValue("$manifest", manifestJson);
            command.Parameters.AddWithValue("$icon", (object?)iconFilename ?? DBNull.Value);
            command.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));

            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    ///     Invalidates manifest cache entry for a specific source path.
    /// </summary>
    public void InvalidateManifest(string sourcePath)
    {
        if (!_initialized || _connection is null) return;

        var normalizedPath = NormalizePath(sourcePath);

        lock (_connectionLock)
        {
            // Get icon filename first to delete the file
            string? iconFilename = null;
            using (var selectCommand = _connection.CreateCommand())
            {
                selectCommand.CommandText = "SELECT icon_filename FROM manifest_cache WHERE source_path = $path";
                selectCommand.Parameters.AddWithValue("$path", normalizedPath);
                iconFilename = selectCommand.ExecuteScalar() as string;
            }

            // Delete from database
            using var deleteCommand = _connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM manifest_cache WHERE source_path = $path";
            deleteCommand.Parameters.AddWithValue("$path", normalizedPath);
            deleteCommand.ExecuteNonQuery();

            // Delete icon file if it exists
            if (!string.IsNullOrWhiteSpace(iconFilename))
            {
                var iconPath = GetImagePath(iconFilename);
                if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
                {
                    try
                    {
                        File.Delete(iconPath);
                    }
                    catch (Exception)
                    {
                        // Ignore deletion errors
                    }
                }
            }
        }
    }

    #endregion

    #region Database Cache Methods

    /// <summary>
    ///     Stores database info for a mod.
    /// </summary>
    public async Task StoreDatabaseInfoAsync(
        string modId,
        string? normalizedGameVersion,
        ModDatabaseInfo info,
        string? installedModVersion,
        string? lastModifiedHeader,
        string? etag,
        string? lastModifiedApiValue,
        CancellationToken cancellationToken)
    {
        if (!_initialized || _connection is null) return;
        if (string.IsNullOrWhiteSpace(modId)) return;
        if (info is null) return;

        var gameVersion = string.IsNullOrWhiteSpace(normalizedGameVersion) ? "any" : normalizedGameVersion;

        // Store logo image if present
        string? logoFilename = null;
        if (!string.IsNullOrWhiteSpace(info.LogoUrl))
        {
            logoFilename = await StoreImageFromUrlAsync(info.LogoUrl, modId, "logo", cancellationToken).ConfigureAwait(false);
        }

        // Load existing tags by version
        var tagsByVersion = LoadExistingTagsByVersion(modId, gameVersion);
        
        // Update tags for installed version if provided
        var normalizedVersion = string.IsNullOrWhiteSpace(installedModVersion) ? null : VersionStringUtility.Normalize(installedModVersion);
        if (!string.IsNullOrWhiteSpace(normalizedVersion) && info.Tags is { Count: > 0 })
        {
            tagsByVersion[normalizedVersion!] = info.Tags.ToArray();
        }

        var tagsJson = info.Tags is { Count: > 0 } 
            ? JsonSerializer.Serialize(info.Tags, SerializerOptions) 
            : null;
        
        var tagsByVersionJson = tagsByVersion.Count > 0
            ? JsonSerializer.Serialize(tagsByVersion, SerializerOptions)
            : null;

        var requiredGameVersionsJson = info.RequiredGameVersions is { Count: > 0 }
            ? JsonSerializer.Serialize(info.RequiredGameVersions, SerializerOptions)
            : null;

        var releasesJson = info.Releases is { Count: > 0 }
            ? JsonSerializer.Serialize(info.Releases, SerializerOptions)
            : null;

        lock (_connectionLock)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO database_cache 
                (mod_id, game_version, tags_json, tags_by_version_json,
                 asset_id, mod_page_url, latest_compatible_version, latest_version,
                 required_game_versions_json, downloads, comments, follows, trending_points,
                 logo_filename, downloads_last_30_days, downloads_last_10_days,
                 last_released_utc, created_utc, releases_json, side,
                 last_modified_header, etag, last_modified_api_value, cached_at)
                VALUES 
                ($modId, $gameVersion, $tags, $tagsByVersion,
                 $assetId, $modPageUrl, $latestCompatible, $latestVersion,
                 $requiredGameVersions, $downloads, $comments, $follows, $trendingPoints,
                 $logo, $downloads30, $downloads10,
                 $lastReleased, $created, $releases, $side,
                 $lastModHeader, $etag, $lastModApi, $cachedAt)";
            
            command.Parameters.AddWithValue("$modId", modId);
            command.Parameters.AddWithValue("$gameVersion", gameVersion);
            command.Parameters.AddWithValue("$tags", (object?)tagsJson ?? DBNull.Value);
            command.Parameters.AddWithValue("$tagsByVersion", (object?)tagsByVersionJson ?? DBNull.Value);
            command.Parameters.AddWithValue("$assetId", (object?)info.AssetId ?? DBNull.Value);
            command.Parameters.AddWithValue("$modPageUrl", (object?)info.ModPageUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("$latestCompatible", (object?)info.LatestCompatibleVersion ?? DBNull.Value);
            command.Parameters.AddWithValue("$latestVersion", (object?)info.LatestVersion ?? DBNull.Value);
            command.Parameters.AddWithValue("$requiredGameVersions", (object?)requiredGameVersionsJson ?? DBNull.Value);
            command.Parameters.AddWithValue("$downloads", (object?)info.Downloads ?? DBNull.Value);
            command.Parameters.AddWithValue("$comments", (object?)info.Comments ?? DBNull.Value);
            command.Parameters.AddWithValue("$follows", (object?)info.Follows ?? DBNull.Value);
            command.Parameters.AddWithValue("$trendingPoints", (object?)info.TrendingPoints ?? DBNull.Value);
            command.Parameters.AddWithValue("$logo", (object?)logoFilename ?? DBNull.Value);
            command.Parameters.AddWithValue("$downloads30", (object?)info.DownloadsLastThirtyDays ?? DBNull.Value);
            command.Parameters.AddWithValue("$downloads10", (object?)info.DownloadsLastTenDays ?? DBNull.Value);
            command.Parameters.AddWithValue("$lastReleased", (object?)info.LastReleasedUtc?.ToString("O") ?? DBNull.Value);
            command.Parameters.AddWithValue("$created", (object?)info.CreatedUtc?.ToString("O") ?? DBNull.Value);
            command.Parameters.AddWithValue("$releases", (object?)releasesJson ?? DBNull.Value);
            command.Parameters.AddWithValue("$side", (object?)info.Side ?? DBNull.Value);
            command.Parameters.AddWithValue("$lastModHeader", (object?)lastModifiedHeader ?? DBNull.Value);
            command.Parameters.AddWithValue("$etag", (object?)etag ?? DBNull.Value);
            command.Parameters.AddWithValue("$lastModApi", (object?)lastModifiedApiValue ?? DBNull.Value);
            command.Parameters.AddWithValue("$cachedAt", DateTimeOffset.UtcNow.ToString("O"));

            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    ///     Attempts to load cached database info for a mod.
    /// </summary>
    public ModDatabaseInfo? TryLoadDatabaseInfo(
        string modId,
        string? normalizedGameVersion,
        string? installedModVersion)
    {
        if (!_initialized || _connection is null) return null;
        if (string.IsNullOrWhiteSpace(modId)) return null;

        var gameVersion = string.IsNullOrWhiteSpace(normalizedGameVersion) ? "any" : normalizedGameVersion;

        lock (_connectionLock)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT tags_json, tags_by_version_json, asset_id, mod_page_url, 
                       latest_compatible_version, latest_version,
                       required_game_versions_json, downloads, comments, follows, trending_points,
                       logo_filename, downloads_last_30_days, downloads_last_10_days,
                       last_released_utc, created_utc, releases_json, side, cached_at
                FROM database_cache
                WHERE mod_id = $modId AND game_version = $gameVersion
                LIMIT 1";
            
            command.Parameters.AddWithValue("$modId", modId);
            command.Parameters.AddWithValue("$gameVersion", gameVersion);

            using var reader = command.ExecuteReader();
            if (!reader.Read()) return null;

            // Parse tags - prefer version-specific tags if available
            var tagsJson = reader.IsDBNull(0) ? null : reader.GetString(0);
            var tagsByVersionJson = reader.IsDBNull(1) ? null : reader.GetString(1);
            
            string[]? tags = null;
            string? cachedTagsVersion = null;
            
            // Try to get version-specific tags first
            if (!string.IsNullOrWhiteSpace(tagsByVersionJson) && !string.IsNullOrWhiteSpace(installedModVersion))
            {
                var normalizedVersion = VersionStringUtility.Normalize(installedModVersion);
                try
                {
                    var tagsByVersion = JsonSerializer.Deserialize<Dictionary<string, string[]>>(tagsByVersionJson, SerializerOptions);
                    if (tagsByVersion != null && !string.IsNullOrWhiteSpace(normalizedVersion) && tagsByVersion.TryGetValue(normalizedVersion, out var versionTags))
                    {
                        tags = versionTags;
                        cachedTagsVersion = normalizedVersion;
                    }
                }
                catch (Exception)
                {
                    // Fall back to general tags
                }
            }

            // Fall back to general tags if no version-specific tags found
            if (tags == null && !string.IsNullOrWhiteSpace(tagsJson))
            {
                try
                {
                    tags = JsonSerializer.Deserialize<string[]>(tagsJson, SerializerOptions);
                }
                catch (Exception)
                {
                    tags = null;
                }
            }

            var requiredGameVersionsJson = reader.IsDBNull(6) ? null : reader.GetString(6);
            var requiredGameVersions = string.IsNullOrWhiteSpace(requiredGameVersionsJson)
                ? Array.Empty<string>()
                : JsonSerializer.Deserialize<string[]>(requiredGameVersionsJson, SerializerOptions) ?? Array.Empty<string>();

            var releasesJson = reader.IsDBNull(16) ? null : reader.GetString(16);
            var releases = string.IsNullOrWhiteSpace(releasesJson)
                ? Array.Empty<ModReleaseInfo>()
                : JsonSerializer.Deserialize<ModReleaseInfo[]>(releasesJson, SerializerOptions) ?? Array.Empty<ModReleaseInfo>();

            return new ModDatabaseInfo
            {
                Tags = tags ?? Array.Empty<string>(),
                CachedTagsVersion = cachedTagsVersion,
                AssetId = reader.IsDBNull(2) ? null : reader.GetString(2),
                ModPageUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
                LatestCompatibleVersion = reader.IsDBNull(4) ? null : reader.GetString(4),
                LatestVersion = reader.IsDBNull(5) ? null : reader.GetString(5),
                RequiredGameVersions = requiredGameVersions,
                Downloads = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                Comments = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                Follows = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                TrendingPoints = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                LogoUrl = null, // Logo is stored as file, not URL
                DownloadsLastThirtyDays = reader.IsDBNull(12) ? null : reader.GetInt32(12),
                DownloadsLastTenDays = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                LastReleasedUtc = reader.IsDBNull(14) ? null : TryParseDateTime(reader.GetString(14)),
                CreatedUtc = reader.IsDBNull(15) ? null : TryParseDateTime(reader.GetString(15)),
                Releases = releases,
                Side = reader.IsDBNull(17) ? null : reader.GetString(17)
            };
        }
    }

    /// <summary>
    ///     Gets cached HTTP headers for conditional requests.
    /// </summary>
    public (string? LastModifiedHeader, string? ETag, string? LastModifiedApiValue, DateTimeOffset? CachedAt) GetCachedHttpHeaders(
        string modId,
        string? normalizedGameVersion)
    {
        if (!_initialized || _connection is null) 
            return (null, null, null, null);
        if (string.IsNullOrWhiteSpace(modId)) 
            return (null, null, null, null);

        var gameVersion = string.IsNullOrWhiteSpace(normalizedGameVersion) ? "any" : normalizedGameVersion;

        lock (_connectionLock)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT last_modified_header, etag, last_modified_api_value, cached_at
                FROM database_cache
                WHERE mod_id = $modId AND game_version = $gameVersion
                LIMIT 1";
            
            command.Parameters.AddWithValue("$modId", modId);
            command.Parameters.AddWithValue("$gameVersion", gameVersion);

            using var reader = command.ExecuteReader();
            if (!reader.Read()) return (null, null, null, null);

            var lastModHeader = reader.IsDBNull(0) ? null : reader.GetString(0);
            var etag = reader.IsDBNull(1) ? null : reader.GetString(1);
            var lastModApi = reader.IsDBNull(2) ? null : reader.GetString(2);
            var cachedAtStr = reader.IsDBNull(3) ? null : reader.GetString(3);
            
            DateTimeOffset? cachedAt = null;
            if (!string.IsNullOrWhiteSpace(cachedAtStr))
            {
                cachedAt = DateTimeOffset.TryParse(cachedAtStr, out var result) ? result : null;
            }

            return (lastModHeader, etag, lastModApi, cachedAt);
        }
    }

    /// <summary>
    ///     Updates tags for a specific mod version (for manifest cache compatibility).
    /// </summary>
    public void UpdateTags(string modId, string? version, IReadOnlyList<string> tags)
    {
        if (!_initialized || _connection is null) return;
        if (string.IsNullOrWhiteSpace(modId)) return;
        if (tags is null || tags.Count == 0) return;

        // For now, we'll update tags in all game versions for this mod
        // This maintains compatibility with the old manifest cache behavior
        lock (_connectionLock)
        {
            using var selectCommand = _connection.CreateCommand();
            selectCommand.CommandText = "SELECT game_version, tags_by_version_json FROM database_cache WHERE mod_id = $modId";
            selectCommand.Parameters.AddWithValue("$modId", modId);

            var updates = new List<(string GameVersion, string TagsByVersionJson)>();
            using (var reader = selectCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    var gameVersion = reader.GetString(0);
                    var tagsByVersionJson = reader.IsDBNull(1) ? null : reader.GetString(1);

                    var tagsByVersion = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrWhiteSpace(tagsByVersionJson))
                    {
                        try
                        {
                            var existing = JsonSerializer.Deserialize<Dictionary<string, string[]>>(tagsByVersionJson, SerializerOptions);
                            if (existing != null)
                            {
                                tagsByVersion = new Dictionary<string, string[]>(existing, StringComparer.OrdinalIgnoreCase);
                            }
                        }
                        catch (Exception)
                        {
                            // Ignore parse errors
                        }
                    }

                    var normalizedVersion = NormalizeVersionWithFallback(version);
                    tagsByVersion[normalizedVersion] = tags.ToArray();

                    var updatedJson = JsonSerializer.Serialize(tagsByVersion, SerializerOptions);
                    updates.Add((gameVersion, updatedJson));
                }
            }

            // Apply updates
            foreach (var (gameVersion, tagsByVersionJson) in updates)
            {
                using var updateCommand = _connection.CreateCommand();
                updateCommand.CommandText = @"
                    UPDATE database_cache 
                    SET tags_by_version_json = $tagsByVersion
                    WHERE mod_id = $modId AND game_version = $gameVersion";
                
                updateCommand.Parameters.AddWithValue("$modId", modId);
                updateCommand.Parameters.AddWithValue("$gameVersion", gameVersion);
                updateCommand.Parameters.AddWithValue("$tagsByVersion", tagsByVersionJson);
                updateCommand.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    ///     Gets tags for a specific mod version (for manifest cache compatibility).
    /// </summary>
    public IReadOnlyList<string> GetTags(string modId, string? version)
    {
        if (!_initialized || _connection is null) return Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(modId)) return Array.Empty<string>();

        var normalizedVersion = NormalizeVersionWithFallback(version);

        lock (_connectionLock)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT tags_by_version_json, tags_json
                FROM database_cache
                WHERE mod_id = $modId
                LIMIT 1";
            
            command.Parameters.AddWithValue("$modId", modId);

            using var reader = command.ExecuteReader();
            if (!reader.Read()) return Array.Empty<string>();

            var tagsByVersionJson = reader.IsDBNull(0) ? null : reader.GetString(0);
            if (!string.IsNullOrWhiteSpace(tagsByVersionJson))
            {
                try
                {
                    var tagsByVersion = JsonSerializer.Deserialize<Dictionary<string, string[]>>(tagsByVersionJson, SerializerOptions);
                    if (tagsByVersion != null && tagsByVersion.TryGetValue(normalizedVersion, out var versionTags))
                    {
                        return versionTags;
                    }
                }
                catch (Exception)
                {
                    // Fall through to general tags
                }
            }

            var tagsJson = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (!string.IsNullOrWhiteSpace(tagsJson))
            {
                try
                {
                    var tags = JsonSerializer.Deserialize<string[]>(tagsJson, SerializerOptions);
                    if (tags != null) return tags;
                }
                catch (Exception)
                {
                    // Ignore
                }
            }

            return Array.Empty<string>();
        }
    }

    private Dictionary<string, string[]> LoadExistingTagsByVersion(
        string modId,
        string gameVersion)
    {
        if (!_initialized || _connection is null) 
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        lock (_connectionLock)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT tags_by_version_json
                FROM database_cache
                WHERE mod_id = $modId AND game_version = $gameVersion
                LIMIT 1";
            
            command.Parameters.AddWithValue("$modId", modId);
            command.Parameters.AddWithValue("$gameVersion", gameVersion);

            var tagsByVersionJson = command.ExecuteScalar() as string;
            if (!string.IsNullOrWhiteSpace(tagsByVersionJson))
            {
                try
                {
                    var existing = JsonSerializer.Deserialize<Dictionary<string, string[]>>(tagsByVersionJson, SerializerOptions);
                    if (existing != null)
                    {
                        return new Dictionary<string, string[]>(existing, StringComparer.OrdinalIgnoreCase);
                    }
                }
                catch (Exception)
                {
                    // Ignore parse errors
                }
            }

            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    ///     Gets the cached latest version for a mod.
    /// </summary>
    public string? GetCachedLatestVersion(string modId, string? normalizedGameVersion)
    {
        if (!_initialized || _connection is null) return null;
        if (string.IsNullOrWhiteSpace(modId)) return null;

        var gameVersion = string.IsNullOrWhiteSpace(normalizedGameVersion) ? "any" : normalizedGameVersion;

        lock (_connectionLock)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT latest_version
                FROM database_cache
                WHERE mod_id = $modId AND game_version = $gameVersion
                LIMIT 1";
            
            command.Parameters.AddWithValue("$modId", modId);
            command.Parameters.AddWithValue("$gameVersion", gameVersion);

            return command.ExecuteScalar() as string;
        }
    }

    /// <summary>
    ///     Gets the full file path to a cached logo image for a mod.
    /// </summary>
    public string? GetLogoPath(string modId, string? normalizedGameVersion)
    {
        if (!_initialized || _connection is null) return null;
        if (string.IsNullOrWhiteSpace(modId)) return null;

        var gameVersion = string.IsNullOrWhiteSpace(normalizedGameVersion) ? "any" : normalizedGameVersion;

        lock (_connectionLock)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT logo_filename
                FROM database_cache
                WHERE mod_id = $modId AND game_version = $gameVersion
                LIMIT 1";
            
            command.Parameters.AddWithValue("$modId", modId);
            command.Parameters.AddWithValue("$gameVersion", gameVersion);

            var logoFilename = command.ExecuteScalar() as string;
            if (string.IsNullOrWhiteSpace(logoFilename)) return null;

            var imagePath = GetImagePath(logoFilename);
            return !string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath) ? imagePath : null;
        }
    }

    /// <summary>
    ///     Attempts to retrieve cached logo image bytes for a mod.
    /// </summary>
    public byte[]? TryGetLogoBytes(string modId, string? normalizedGameVersion)
    {
        var logoPath = GetLogoPath(modId, normalizedGameVersion);
        if (string.IsNullOrWhiteSpace(logoPath)) return null;

        try
        {
            return File.ReadAllBytes(logoPath);
        }
        catch (Exception)
        {
            return null;
        }
    }

    #endregion

    #region Image Storage

    private static async Task<string?> StoreImageFromUrlAsync(string imageUrl, string modId, string suffix, CancellationToken cancellationToken)
    {
        var imageBytes = await ModImageCacheService.TryGetCachedImageAsync(imageUrl, cancellationToken).ConfigureAwait(false);
        
        if (imageBytes is not { Length: > 0 })
        {
            // Download if not cached
            try
            {
                imageBytes = await _httpClient.GetByteArrayAsync(imageUrl, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Re-throw cancellation (includes TaskCanceledException for timeout)
                throw;
            }
            catch (System.Net.Http.HttpRequestException)
            {
                // Network or HTTP error - image not available
                return null;
            }
        }

        if (imageBytes is null or { Length: 0 }) return null;

        var extension = GetImageExtension(imageUrl);
        var filename = $"{SanitizeForFilename(modId)}_{suffix}{extension}";
        var imagePath = GetImagePath(filename);
        
        if (string.IsNullOrWhiteSpace(imagePath)) return null;

        try
        {
            var directory = Path.GetDirectoryName(imagePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllBytesAsync(imagePath, imageBytes, cancellationToken).ConfigureAwait(false);
            return filename;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string GetImageExtension(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return ".png";

        try
        {
            var urlWithoutQuery = imageUrl;
            var queryIndex = imageUrl.IndexOf('?', StringComparison.Ordinal);
            if (queryIndex >= 0)
            {
                urlWithoutQuery = imageUrl.Substring(0, queryIndex);
            }

            var extension = Path.GetExtension(urlWithoutQuery);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                extension = extension.ToLowerInvariant();
                if (extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp")
                {
                    return extension;
                }
            }
        }
        catch (Exception)
        {
            // Ignore parsing errors
        }

        return ".png";
    }

    #endregion

    #region Cache Management

    /// <summary>
    ///     Clears all cache data including database and images.
    /// </summary>
    public void ClearCache()
    {
        if (!_initialized || _connection is null) return;

        lock (_connectionLock)
        {
            using var command1 = _connection.CreateCommand();
            command1.CommandText = "DELETE FROM manifest_cache";
            command1.ExecuteNonQuery();

            using var command2 = _connection.CreateCommand();
            command2.CommandText = "DELETE FROM database_cache";
            command2.ExecuteNonQuery();
        }

        // Clear images directory
        var imagesDir = GetImagesDirectory();
        if (!string.IsNullOrWhiteSpace(imagesDir) && Directory.Exists(imagesDir))
        {
            try
            {
                Directory.Delete(imagesDir, true);
            }
            catch (Exception)
            {
                // Ignore cleanup errors
            }
        }
    }

    #endregion

    #region Helper Methods

    private static string GetVersionOrDefault(string? version, string defaultValue = "unknown")
    {
        return string.IsNullOrWhiteSpace(version) ? defaultValue : version;
    }

    private static string NormalizeVersionWithFallback(string? version, string defaultValue = "unknown")
    {
        if (string.IsNullOrWhiteSpace(version)) return defaultValue;
        return VersionStringUtility.Normalize(version) ?? version;
    }

    private static DateTime? TryParseDateTime(string? dateTimeString)
    {
        if (string.IsNullOrWhiteSpace(dateTimeString)) return null;
        
        return DateTime.TryParse(dateTimeString, out var result) ? result : null;
    }

    private static string? GetDatabasePath()
    {
        var baseDirectory = ModCacheLocator.GetManagerDataDirectory();
        if (string.IsNullOrWhiteSpace(baseDirectory)) return null;

        var cacheDir = Path.Combine(baseDirectory, "Temp Cache");
        return Path.Combine(cacheDir, DatabaseFileName);
    }

    private static string? GetImagesDirectory()
    {
        var baseDirectory = ModCacheLocator.GetManagerDataDirectory();
        if (string.IsNullOrWhiteSpace(baseDirectory)) return null;

        return Path.Combine(baseDirectory, "Temp Cache", "Images");
    }

    private static string? GetImagePath(string filename)
    {
        var imagesDir = GetImagesDirectory();
        if (string.IsNullOrWhiteSpace(imagesDir)) return null;

        return Path.Combine(imagesDir, filename);
    }

    private static string NormalizePath(string sourcePath)
    {
        try
        {
            return Path.GetFullPath(sourcePath);
        }
        catch (Exception)
        {
            return sourcePath;
        }
    }

    private static long ToUniversalTicks(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
            value = DateTime.SpecifyKind(value, DateTimeKind.Local);

        return value.ToUniversalTime().Ticks;
    }

    private static string SanitizeForFilename(string input)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var result = new char[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            result[i] = Array.IndexOf(invalidChars, input[i]) >= 0 ? '_' : input[i];
        }
        return new string(result);
    }

    #endregion

    public void Dispose()
    {
        lock (_connectionLock)
        {
            _connection?.Dispose();
            _connection = null;
            _initialized = false;
        }
    }
}
