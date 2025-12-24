# Mod Loading Performance Timing Feature

## Overview
This feature tracks the cumulative time spent on various mod loading operations throughout the lifetime of the application. When the application exits, a summary of all timing metrics is automatically saved to the log file.

## Tracked Operations

The following operations are tracked for each mod:

1. **Icon Loading** - Time spent loading mod icon images
2. **Tags Loading** - Time spent loading and processing mod tags from the database
3. **User Reports/Votes Loading** - Time spent loading user feedback and vote summaries
4. **Dependency/Error/Warning Checks** - Time spent validating mod dependencies and checking for issues
5. **Update Checks** - Time spent checking if newer versions are available
6. **Changelog Loading** - Time spent loading and processing release changelogs
7. **Database Info Loading** - Time spent loading general metadata from the mod database
   - **Cache Loading** - Time spent loading database info from cache
   - **Network Loading** - Time spent fetching database info from the network/API
     - **HTTP Request/Response** - Time spent on HTTP request and receiving response
     - **JSON Parsing** - Time spent parsing JSON response
     - **Data Extraction** - Time spent extracting and processing data from parsed JSON
     - **Cache Storage** - Time spent storing fetched data to cache
   - **Applying Info** - Time spent applying database info to mod entries
     - **Dispatcher Wait** - Time spent waiting for UI thread dispatcher
     - **Entry Update** - Time spent updating mod entry with database info
     - **ViewModel Update** - Time spent updating view model with database info
   - **Offline Info Population** - Time spent creating offline database info

## How It Works

### Data Collection
- All timing data is accumulated **cumulatively** across the entire application session
- Times are tracked regardless of whether operations occur during:
  - Initial mod loading
  - Fast check operations
  - Manual refresh operations
  - Database updates

### Timing Mechanism
- Each operation is measured using `System.Diagnostics.Stopwatch`
- Measurements are thread-safe and can be called from multiple threads simultaneously
- The timing service uses disposable scopes for convenient automatic timing:
  ```csharp
  using (_timingService?.MeasureIconLoad())
  {
      // Code to load icon
  }
  // Time is automatically recorded when the scope is disposed
  ```

### Output Format
On application exit (if error/diagnostic logging is enabled via the "Enable Logging -> Exceptions and errors" menu option), the timing summary is written to `Simple VS Manager logs.txt` in the following format:

```
=== Mod Loading Performance Metrics ===

Icon Loading: 2.45s total (150 ops, avg 16.33ms/op)
Tags Loading: 1.23s total (150 ops, avg 8.20ms/op)
User Reports/Votes Loading: 5.67s total (150 ops, avg 37.80ms/op)
Dependency/Error/Warning Checks: 0.89s total (150 ops, avg 5.93ms/op)
Update Checks: 1.45s total (150 ops, avg 9.67ms/op)
Changelog Loading: 0.56s total (150 ops, avg 3.73ms/op)
Database Info Loading: 45.23s total (150 ops, avg 301.53ms/op)

  Database Info Loading Breakdown:
  Cache Loading: 4.24s total (384 ops, avg 11.04ms/op)
  Network Loading: 38.50s total (150 ops, avg 256.67ms/op)

    Network Loading Breakdown:
    HTTP Request/Response: 30.10s total (150 ops, avg 200.67ms/op)
    JSON Parsing: 2.15s total (150 ops, avg 14.33ms/op)
    Data Extraction: 4.80s total (150 ops, avg 32.00ms/op)
    Cache Storage: 1.45s total (150 ops, avg 9.67ms/op)

  Applying Info: 1.23s total (384 ops, avg 3.20ms/op)

    Applying Info Breakdown:
    Dispatcher Wait: 0.45s total (384 ops, avg 1.17ms/op)
    Entry Update: 0.38s total (384 ops, avg 0.99ms/op)
    ViewModel Update: 0.40s total (384 ops, avg 1.04ms/op)

  Offline Info Population: 1.26s total (84 ops, avg 15.00ms/op)

Total Time Across All Operations: 57.48s
=======================================
```

Note: The Database Info Loading breakdown and its sub-breakdowns are only shown when sub-operations are measured.

## Implementation Details

### New Components
- `ModLoadingTimingService` - Main service class that tracks all timing metrics
- Integration points in `ModListItemViewModel` for per-mod operations
- Integration points in `MainViewModel` for database refresh operations
- Logging integration in `ModActivityLoggingService` and `MainWindow`

### Modified Components
- `ModListItemViewModel` - Added timing measurements for icon, tags, user reports, dependencies, updates, and changelogs
- `MainViewModel` - Added timing measurements for database info loading and exposed timing service
- `ModActivityLoggingService` - Added method to log timing summary
- `MainWindow.xaml.cs` - Added code to save timing summary on application exit

## Usage for Developers

To measure additional operations in the future:

1. Add a new method to `ModLoadingTimingService`:
   ```csharp
   public void RecordMyOperationTime(double milliseconds)
   {
       lock (_lock)
       {
           _totalMyOperationTimeMs += milliseconds;
           _myOperationCount++;
       }
   }
   
   public IDisposable MeasureMyOperation()
   {
       return new TimingScope(this, RecordMyOperationTime);
   }
   ```

2. Use the timing scope where the operation occurs:
   ```csharp
   using (_timingService?.MeasureMyOperation())
   {
       // Your code here
   }
   ```

3. Update `GetTimingSummary()` to include the new metric in the output

## Configuration

The timing feature is always enabled and automatically logs when the application exits, **but only if error/diagnostic logging is enabled** in user configuration (`LogErrorsAndExceptions` setting, accessible via the "Enable Logging -> Exceptions and errors" menu option).

## Performance Impact

The timing feature has minimal performance impact:
- Stopwatch operations are very fast (microseconds)
- Locking is used efficiently with minimal contention
- No disk I/O occurs during measurement (only on app exit)
- The feature can be easily disabled by passing `null` as the timing service parameter

## Testing

To verify the timing feature is working:

1. Launch the application
2. Load some mods (initial load)
3. Perform a refresh operation
4. Exit the application
5. Check `Simple VS Manager logs.txt` for the timing summary near the end of the file

The summary will appear just before the "App exited" log entry.
