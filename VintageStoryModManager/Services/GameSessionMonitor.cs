using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace VintageStoryModManager.Services;

/// <summary>
/// Monitors Vintage Story log files to detect long-running game sessions.
/// </summary>
public sealed class GameSessionMonitor : IDisposable
{
    private enum LogCategory
    {
        Client,
        Server
    }

    private sealed class LogTailState
    {
        public LogTailState(long initialPosition)
        {
            Position = initialPosition < 0 ? 0 : initialPosition;
        }

        public long Position { get; set; }

        public object SyncRoot { get; } = new();
    }

    private static readonly TimeSpan MinimumSessionDuration = DevConfig.MinimumSessionDuration;

    private static readonly string[] ClientStartMarkers =
    {
        "received level finalize",
        "game launch can proceed"
    };

    private static readonly string[] ClientEndMarkers =
    {
        "destroying game session",
        "stopping single player server"
    };

    private static readonly string[] ServerEndMarkers =
    {
        "server shutting down"
    };

    private readonly string _logsDirectory;
    private readonly Dispatcher _dispatcher;
    private readonly UserConfigurationService _configuration;
    private readonly Func<IReadOnlyList<string>> _activeModProvider;
    private readonly ConcurrentDictionary<string, LogTailState> _logStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sessionLock = new();

    private FileSystemWatcher? _clientWatcher;
    private FileSystemWatcher? _serverWatcher;
    private bool _sessionActive;
    private DateTimeOffset _sessionStartUtc;
    private List<string> _sessionModIds = new();
    private bool _disposed;
    private bool _lastPromptState;

    public GameSessionMonitor(
        string logsDirectory,
        Dispatcher dispatcher,
        UserConfigurationService configuration,
        Func<IReadOnlyList<string>> activeModProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logsDirectory);
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _activeModProvider = activeModProvider ?? throw new ArgumentNullException(nameof(activeModProvider));

        _logsDirectory = Path.GetFullPath(logsDirectory);

        try
        {
            Directory.CreateDirectory(_logsDirectory);
            InitializeWatchers();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusLogService.AppendStatus(
                string.Format(CultureInfo.CurrentCulture, "Failed to initialize log monitor: {0}", ex.Message),
                true);
        }

        _lastPromptState = _configuration.HasPendingModUsagePrompt;
    }

    public event EventHandler? PromptRequired;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _clientWatcher?.Dispose();
        _clientWatcher = null;
        _serverWatcher?.Dispose();
        _serverWatcher = null;
    }

    public void RefreshPromptState()
    {
        _lastPromptState = _configuration.HasPendingModUsagePrompt;
    }

    private void InitializeWatchers()
    {
        _clientWatcher = CreateWatcher("client-main*");
        if (_clientWatcher is not null)
        {
            _clientWatcher.Changed += (_, e) => HandleLogEvent(e.FullPath, LogCategory.Client);
            _clientWatcher.Created += (_, e) => HandleLogEvent(e.FullPath, LogCategory.Client);
            _clientWatcher.Renamed += (_, e) => HandleLogEvent(e.FullPath, LogCategory.Client);
            _clientWatcher.Renamed += (_, e) => RemoveLogState(e.OldFullPath);
            _clientWatcher.Deleted += (_, e) => RemoveLogState(e.FullPath);
            _clientWatcher.EnableRaisingEvents = true;
            InitializeExistingLogs(LogCategory.Client);
        }

        _serverWatcher = CreateWatcher("server-main*");
        if (_serverWatcher is not null)
        {
            _serverWatcher.Changed += (_, e) => HandleLogEvent(e.FullPath, LogCategory.Server);
            _serverWatcher.Created += (_, e) => HandleLogEvent(e.FullPath, LogCategory.Server);
            _serverWatcher.Renamed += (_, e) => HandleLogEvent(e.FullPath, LogCategory.Server);
            _serverWatcher.Renamed += (_, e) => RemoveLogState(e.OldFullPath);
            _serverWatcher.Deleted += (_, e) => RemoveLogState(e.FullPath);
            _serverWatcher.EnableRaisingEvents = true;
            InitializeExistingLogs(LogCategory.Server);
        }
    }

    private FileSystemWatcher? CreateWatcher(string filter)
    {
        try
        {
            return new FileSystemWatcher(_logsDirectory, filter)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
                EnableRaisingEvents = false
            };
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void InitializeExistingLogs(LogCategory category)
    {
        try
        {
            foreach (string path in EnumerateCategoryFiles(category))
            {
                EnsureLogState(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusLogService.AppendStatus(
                string.Format(CultureInfo.CurrentCulture, "Failed to enumerate existing logs: {0}", ex.Message),
                true);
        }
    }

    private IEnumerable<string> EnumerateCategoryFiles(LogCategory category)
    {
        string prefix = category == LogCategory.Client ? "client-main" : "server-main";

        IEnumerable<string> Enumerate(string pattern)
        {
            return Directory.EnumerateFiles(_logsDirectory, pattern, SearchOption.TopDirectoryOnly);
        }

        foreach (string file in Enumerate(prefix + "*.log"))
        {
            yield return file;
        }

        foreach (string file in Enumerate(prefix + "*.txt"))
        {
            yield return file;
        }
    }

    private void HandleLogEvent(string? path, LogCategory category)
    {
        if (_disposed || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _ = Task.Run(() => ProcessLogChangesAsync(category, path));
    }

    private void RemoveLogState(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _logStates.TryRemove(Path.GetFullPath(path), out _);
    }

    private async Task ProcessLogChangesAsync(LogCategory category, string path)
    {
        if (_disposed)
        {
            return;
        }

        string fullPath = Path.GetFullPath(path);
        if (!IsSupportedLog(fullPath))
        {
            return;
        }

        LogTailState state = EnsureLogState(fullPath);

        List<string> newLines = new();
        try
        {
            using FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            lock (state.SyncRoot)
            {
                if (state.Position > stream.Length)
                {
                    state.Position = 0;
                }

                if (stream.Length == state.Position)
                {
                    return;
                }

                stream.Seek(state.Position, SeekOrigin.Begin);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
                while (!reader.EndOfStream)
                {
                    string? line = reader.ReadLine();
                    if (line is null)
                    {
                        break;
                    }

                    if (line.Length > 0)
                    {
                        newLines.Add(line);
                    }
                }

                state.Position = stream.Position;
            }
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        foreach (string line in newLines)
        {
            await HandleLogLineAsync(category, line).ConfigureAwait(false);
        }
    }

    private static bool IsSupportedLog(string path)
    {
        string extension = Path.GetExtension(path);
        if (!extension.Equals(".log", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string fileName = Path.GetFileName(path);
        return fileName.Contains("client-main", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("server-main", StringComparison.OrdinalIgnoreCase);
    }

    private LogTailState EnsureLogState(string path)
    {
        string fullPath = Path.GetFullPath(path);
        return _logStates.GetOrAdd(fullPath, CreateInitialLogState);
    }

    private LogTailState CreateInitialLogState(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return new LogTailState(info.Exists ? info.Length : 0);
        }
        catch (IOException)
        {
            return new LogTailState(0);
        }
        catch (UnauthorizedAccessException)
        {
            return new LogTailState(0);
        }
    }

    private async Task HandleLogLineAsync(LogCategory category, string line)
    {
        if (category == LogCategory.Client)
        {
            if (ContainsMarker(line, ClientStartMarkers))
            {
                await StartSessionAsync().ConfigureAwait(false);
                return;
            }

            if (ContainsMarker(line, ClientEndMarkers))
            {
                await CompleteSessionAsync("client-main").ConfigureAwait(false);
                return;
            }
        }
        else if (category == LogCategory.Server)
        {
            if (ContainsMarker(line, ServerEndMarkers))
            {
                await CompleteSessionAsync("server-main").ConfigureAwait(false);
            }
        }
    }

    private static bool ContainsMarker(string line, IReadOnlyList<string> markers)
    {
        foreach (string marker in markers)
        {
            if (line.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private async Task StartSessionAsync()
    {
        IReadOnlyList<string>? activeModIds = null;
        try
        {
            activeModIds = await _dispatcher.InvokeAsync(_activeModProvider);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        lock (_sessionLock)
        {
            if (_sessionActive && _sessionStartUtc != default)
            {
                return;
            }

            _sessionActive = true;
            _sessionStartUtc = DateTimeOffset.UtcNow;
            _sessionModIds = NormalizeModIds(activeModIds);
        }

        StatusLogService.AppendStatus("Detected Vintage Story game session start from logs.", false);
    }

    private async Task CompleteSessionAsync(string source)
    {
        List<string> modIds;
        DateTimeOffset start;
        lock (_sessionLock)
        {
            if (!_sessionActive)
            {
                return;
            }

            _sessionActive = false;
            start = _sessionStartUtc;
            modIds = _sessionModIds;
            _sessionStartUtc = default;
            _sessionModIds = new List<string>();
        }

        if (start == default)
        {
            return;
        }

        DateTimeOffset end = DateTimeOffset.UtcNow;
        TimeSpan duration = end - start;
        if (duration < MinimumSessionDuration)
        {
            StatusLogService.AppendStatus(
                string.Format(
                    CultureInfo.CurrentCulture,
                    "Game session ended after {0:F1} minutes (source: {1}); below 20 minute threshold.",
                    duration.TotalMinutes,
                    source),
                false);
            return;
        }

        bool shouldPrompt = false;
        try
        {
            shouldPrompt = await _dispatcher.InvokeAsync(() => _configuration.RecordLongRunningSession(modIds));
        }
        catch (TaskCanceledException)
        {
            return;
        }

        StatusLogService.AppendStatus(
            string.Format(
                CultureInfo.CurrentCulture,
                "Game session lasted {0:F1} minutes; recorded for mod usage tracking (source: {1}).",
                duration.TotalMinutes,
                source),
            false);

        bool isPending = _configuration.HasPendingModUsagePrompt;
        if (shouldPrompt && !_lastPromptState)
        {
            _lastPromptState = true;
            PromptRequired?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _lastPromptState = isPending;
        }
    }

    private static List<string> NormalizeModIds(IReadOnlyList<string>? modIds)
    {
        if (modIds is null || modIds.Count == 0)
        {
            return new List<string>();
        }

        var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<string>(modIds.Count);
        foreach (string modId in modIds)
        {
            if (string.IsNullOrWhiteSpace(modId))
            {
                continue;
            }

            string trimmed = modId.Trim();
            if (distinct.Add(trimmed))
            {
                normalized.Add(trimmed);
            }
        }

        return normalized;
    }
}
