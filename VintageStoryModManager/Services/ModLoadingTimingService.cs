using System.Diagnostics;
using System.Globalization;

namespace VintageStoryModManager.Services;

/// <summary>
///     Tracks cumulative timing metrics for mod loading operations.
///     All times are accumulated regardless of whether they occur during initial load, refresh, or fast check.
/// </summary>
public sealed class ModLoadingTimingService
{
    private readonly object _lock = new();
    
    // Cumulative timing metrics (in milliseconds)
    private double _totalIconLoadingTimeMs;
    private double _totalTagsLoadingTimeMs;
    private double _totalUserReportsLoadingTimeMs;
    private double _totalDependencyChecksTimeMs;
    private double _totalUpdateCheckTimeMs;
    private double _totalChangelogLoadingTimeMs;
    private double _totalDatabaseInfoLoadingTimeMs;
    
    // Count of operations for averaging
    private int _iconLoadCount;
    private int _tagsLoadCount;
    private int _userReportsLoadCount;
    private int _dependencyCheckCount;
    private int _updateCheckCount;
    private int _changelogLoadCount;
    private int _databaseInfoLoadCount;

    /// <summary>
    ///     Records time spent loading an icon.
    /// </summary>
    public void RecordIconLoadTime(double milliseconds)
    {
        lock (_lock)
        {
            _totalIconLoadingTimeMs += milliseconds;
            _iconLoadCount++;
        }
    }

    /// <summary>
    ///     Records time spent loading tags.
    /// </summary>
    public void RecordTagsLoadTime(double milliseconds)
    {
        lock (_lock)
        {
            _totalTagsLoadingTimeMs += milliseconds;
            _tagsLoadCount++;
        }
    }

    /// <summary>
    ///     Records time spent loading user reports/votes.
    /// </summary>
    public void RecordUserReportsLoadTime(double milliseconds)
    {
        lock (_lock)
        {
            _totalUserReportsLoadingTimeMs += milliseconds;
            _userReportsLoadCount++;
        }
    }

    /// <summary>
    ///     Records time spent checking dependencies/errors/warnings.
    /// </summary>
    public void RecordDependencyCheckTime(double milliseconds)
    {
        lock (_lock)
        {
            _totalDependencyChecksTimeMs += milliseconds;
            _dependencyCheckCount++;
        }
    }

    /// <summary>
    ///     Records time spent checking for updates.
    /// </summary>
    public void RecordUpdateCheckTime(double milliseconds)
    {
        lock (_lock)
        {
            _totalUpdateCheckTimeMs += milliseconds;
            _updateCheckCount++;
        }
    }

    /// <summary>
    ///     Records time spent loading changelogs.
    /// </summary>
    public void RecordChangelogLoadTime(double milliseconds)
    {
        lock (_lock)
        {
            _totalChangelogLoadingTimeMs += milliseconds;
            _changelogLoadCount++;
        }
    }

    /// <summary>
    ///     Records time spent loading database info (general metadata).
    /// </summary>
    public void RecordDatabaseInfoLoadTime(double milliseconds)
    {
        lock (_lock)
        {
            _totalDatabaseInfoLoadingTimeMs += milliseconds;
            _databaseInfoLoadCount++;
        }
    }

    /// <summary>
    ///     Gets a formatted summary of all timing metrics for logging.
    /// </summary>
    public string GetTimingSummary()
    {
        lock (_lock)
        {
            var lines = new List<string>
            {
                "=== Mod Loading Performance Metrics ===",
                "",
                FormatMetric("Icon Loading", _totalIconLoadingTimeMs, _iconLoadCount),
                FormatMetric("Tags Loading", _totalTagsLoadingTimeMs, _tagsLoadCount),
                FormatMetric("User Reports/Votes Loading", _totalUserReportsLoadingTimeMs, _userReportsLoadCount),
                FormatMetric("Dependency/Error/Warning Checks", _totalDependencyChecksTimeMs, _dependencyCheckCount),
                FormatMetric("Update Checks", _totalUpdateCheckTimeMs, _updateCheckCount),
                FormatMetric("Changelog Loading", _totalChangelogLoadingTimeMs, _changelogLoadCount),
                FormatMetric("Database Info Loading", _totalDatabaseInfoLoadingTimeMs, _databaseInfoLoadCount),
                "",
                $"Total Time Across All Operations: {FormatTime(_totalIconLoadingTimeMs + _totalTagsLoadingTimeMs + _totalUserReportsLoadingTimeMs + _totalDependencyChecksTimeMs + _totalUpdateCheckTimeMs + _totalChangelogLoadingTimeMs + _totalDatabaseInfoLoadingTimeMs)}",
                "======================================="
            };

            return string.Join(Environment.NewLine, lines);
        }
    }

    private static string FormatMetric(string label, double totalMs, int count)
    {
        if (count == 0)
        {
            return $"{label}: No operations recorded";
        }

        var avgMs = totalMs / count;
        return $"{label}: {FormatTime(totalMs)} total ({count} ops, avg {FormatTime(avgMs)}/op)";
    }

    private static string FormatTime(double milliseconds)
    {
        if (milliseconds < 1000)
        {
            return $"{milliseconds:F2}ms";
        }

        var seconds = milliseconds / 1000.0;
        return $"{seconds:F2}s";
    }

    /// <summary>
    ///     Starts a timing operation and returns a disposable that records the elapsed time when disposed.
    /// </summary>
    public IDisposable MeasureIconLoad()
    {
        return new TimingScope(this, RecordIconLoadTime);
    }

    /// <summary>
    ///     Starts a timing operation and returns a disposable that records the elapsed time when disposed.
    /// </summary>
    public IDisposable MeasureTagsLoad()
    {
        return new TimingScope(this, RecordTagsLoadTime);
    }

    /// <summary>
    ///     Starts a timing operation and returns a disposable that records the elapsed time when disposed.
    /// </summary>
    public IDisposable MeasureUserReportsLoad()
    {
        return new TimingScope(this, RecordUserReportsLoadTime);
    }

    /// <summary>
    ///     Starts a timing operation and returns a disposable that records the elapsed time when disposed.
    /// </summary>
    public IDisposable MeasureDependencyCheck()
    {
        return new TimingScope(this, RecordDependencyCheckTime);
    }

    /// <summary>
    ///     Starts a timing operation and returns a disposable that records the elapsed time when disposed.
    /// </summary>
    public IDisposable MeasureUpdateCheck()
    {
        return new TimingScope(this, RecordUpdateCheckTime);
    }

    /// <summary>
    ///     Starts a timing operation and returns a disposable that records the elapsed time when disposed.
    /// </summary>
    public IDisposable MeasureChangelogLoad()
    {
        return new TimingScope(this, RecordChangelogLoadTime);
    }

    /// <summary>
    ///     Starts a timing operation and returns a disposable that records the elapsed time when disposed.
    /// </summary>
    public IDisposable MeasureDatabaseInfoLoad()
    {
        return new TimingScope(this, RecordDatabaseInfoLoadTime);
    }

    private sealed class TimingScope : IDisposable
    {
        private readonly ModLoadingTimingService _service;
        private readonly Action<double> _recordAction;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        public TimingScope(ModLoadingTimingService service, Action<double> recordAction)
        {
            _service = service;
            _recordAction = recordAction;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _stopwatch.Stop();
            _recordAction(_stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
