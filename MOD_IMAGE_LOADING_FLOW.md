# Mod Image Loading Flow - Complete Step-by-Step Documentation

This document traces the complete flow of how mod images are loaded from cache when a mod database search or browse is triggered in Simple VS Manager.

## Overview

The image loading process involves:
1. **ModDatabaseService** - Fetches mod metadata from the API
2. **ModImageCacheService** - Manages cached image files
3. **ModListItemViewModel** - Represents individual mods in the UI
4. **MainViewModel** - Orchestrates the search/browse workflow
5. **MainWindow.xaml** - Displays the images via WPF bindings

---

## Step-by-Step Flow

### Step 1: User Initiates Database Search or Browse

**Location**: `MainWindow.xaml` / `MainViewModel.cs`

**Trigger Events**:
- User switches to the "Mod Database" tab (`MiddleTabControl_OnSelectionChanged`)
- User types in the search box (triggers `SearchText` property change)
- User selects a different auto-load mode (Most Downloaded, Trending, etc.)

**Entry Point**: `MainViewModel.SearchText` property setter or tab change events

**Function Called**: `MainViewModel.TriggerModDatabaseSearchAsync()`

**Code Location**: `MainViewModel.cs` line ~2955-3038

**What Happens**:
- Cancels any ongoing search operations
- Creates a new `CancellationTokenSource`
- Starts a background task that calls `RunModDatabaseSearchAsync()`

---

### Step 2: Execute Database Query

**Function**: `MainViewModel.RunModDatabaseSearchAsync()`

**Code Location**: `MainViewModel.cs` lines 3040-3370

**What Happens**:
1. **Debounce delay** (300ms) to avoid excessive API calls while user is typing
2. **Internet check** - verifies internet access is available
3. **Determine query type**:
   - If user entered search text: `ModDatabaseService.SearchModsWithCacheAsync()`
   - If browsing (no search text): Uses auto-load mode method like:
     - `GetMostDownloadedModsWithCacheAsync()`
     - `GetMostTrendingModsWithCacheAsync()`
     - `GetRecentlyUpdatedModsWithCacheAsync()`
     - etc.

**Code Location**: `MainViewModel.cs` lines 3076-3122

**Example**:
```csharp
results = await _databaseService.SearchModsWithCacheAsync(query, queryLimit, cancellationToken)
    .ConfigureAwait(false);
```

---

### Step 3: ModDatabaseService Query Execution

**Function**: `ModDatabaseService.SearchModsWithCacheAsync()` (or other browse methods)

**Code Location**: `ModDatabaseService.cs` lines 353-374

**What Happens**:
1. **Check search cache** first using `ModDatabaseSearchCacheService`
2. If cached results exist (< 5 minutes old), return them immediately
3. If not cached or expired, make HTTP API call to Vintage Story mod database
4. Parse JSON response into `ModDatabaseSearchResult` objects
5. **Store results in cache** for 5 minutes

**Key Point**: At this stage, only **basic mod metadata** is retrieved, including:
- Mod ID
- Name
- Summary
- Author
- Tags
- Download counts
- **LogoUrl** (the image URL we'll load later)

**Code Location**: `ModDatabaseService.cs` lines 876-935 (`QueryModsAsync`)

**JSON Parsing**: `ModDatabaseService.TryCreateSearchResult()`
**Code Location**: `ModDatabaseService.cs` lines 1395-1472

**Logo URL Extraction**:
```csharp
var logo = GetString(element, "logo");
if (string.IsNullOrWhiteSpace(logo)) 
    logo = GetString(element, "logofile");
```

---

### Step 4: Create Search Result Entries and ViewModels

**Back to**: `MainViewModel.RunModDatabaseSearchAsync()`

**Code Location**: `MainViewModel.cs` lines 3219-3238

**What Happens**:
1. **Create ModEntry objects** from search results (`CreateSearchResultEntry`)
2. **Create ModListItemViewModel objects** for each entry (`CreateSearchResultViewModel`)
3. ViewModels are created on a background thread but will need UI thread for display

**ViewModel Creation**: `MainViewModel.CreateSearchResultViewModel()`
**Code Location**: `MainViewModel.cs` lines ~2734-2781

**Inside ModListItemViewModel Constructor**:
**Code Location**: `ModListItemViewModel.cs` lines 130-191

**What Happens During ViewModel Construction**:
- Stores the **LogoUrl** in `_modDatabaseLogoUrl` field (line 127)
- Creates mod icon from entry bytes if available (line 177)
- Sets `_modDatabaseLogo = null` initially (not yet loaded)

```csharp
_modDatabaseLogoUrl = databaseInfo?.LogoUrl;
Icon = CreateImage(entry.IconBytes, "Icon bytes");
```

---

### Step 5: Populate Database Info from Cache

**Function**: `ModDatabaseService.PopulateModDatabaseInfoAsync()`

**Code Location**: `ModDatabaseService.cs` lines 72-151

**What Happens** (in parallel for all mods):
1. **First: Check disk cache** - `ModDatabaseCacheService.TryLoadWithLastModifiedAsync()`
   - Looks for cached JSON file in `%AppData%\VintageStoryModManager\Cache\ModDatabase\`
   - Cache includes full mod details including all releases
   - Cache expiry: 2 hours (hard) / 5 minutes (soft)
   
2. **If cached and not expired**: Use cached data immediately
   
3. **If no cache or expired**: Make HTTP request to API
   - URL format: `https://mods.vintagestory.at/api/mod/{modId}`
   - Fetches complete mod details including releases, changelogs, downloads
   - **Stores in cache** for future use

**Cache Storage**: `ModDatabaseCacheService.StoreAsync()`
**Code Location**: `ModDatabaseCacheService.cs`

---

### Step 6: Update ViewModels with Database Info

**Function**: `MainViewModel.RunModDatabaseSearchAsync()` continuation

**Code Location**: `MainViewModel.cs` lines 3264-3333

**What Happens**:
1. **Load cached info first** (lines 3264-3304):
   - Parallel load of cached database info for all search results
   - Batch update ViewModels on UI thread with cached data
   - This provides immediate display of basic info

2. **Update from network** (lines 3313-3333):
   - `PopulateModDatabaseInfoAsync()` fetches fresh data from API
   - Updates ViewModels with latest information
   - **Crucially**: This updates the `_modDatabaseLogoUrl` in each ViewModel

**ViewModel Update**: `ModListItemViewModel.UpdateDatabaseInfo()`
**Code Location**: `ModListItemViewModel.cs` lines 990-1130

**Key Updates in UpdateDatabaseInfo**:
```csharp
var logoUrlChanged = !string.Equals(_modDatabaseLogoUrl, logoUrl, StringComparison.Ordinal);
if (logoUrlChanged)
{
    _modDatabaseLogoUrl = logoUrl;
    if (_modDatabaseLogo is not null)
    {
        _modDatabaseLogo = null; // Clear old image when URL changes
        OnPropertyChanged(nameof(ModDatabasePreviewImage));
    }
}
```

---

### Step 7: Load Mod Images from Cache

**Function**: `MainViewModel.LoadModDatabaseLogosAsync()`

**Code Location**: `MainViewModel.cs` lines 3756-3779

**What Happens**:
- Status message: "Loading mod images..."
- Creates parallel tasks for **all** visible mod ViewModels
- Each task calls: `viewModel.LoadModDatabaseLogoAsync(cancellationToken)`
- **Waits for all images** to complete loading

```csharp
var tasks = new List<Task>(viewModels.Count);
foreach (var viewModel in viewModels)
{
    tasks.Add(viewModel.LoadModDatabaseLogoAsync(cancellationToken));
}
await Task.WhenAll(tasks).ConfigureAwait(false);
```

---

### Step 8: Load Individual Mod Image

**Function**: `ModListItemViewModel.LoadModDatabaseLogoAsync()`

**Code Location**: `ModListItemViewModel.cs` lines 1133-1209

This is the **CRITICAL STEP** where images are loaded from cache!

**Detailed Substeps**:

#### 8a. Pre-checks
```csharp
var logoUrl = _modDatabaseLogoUrl;

// Skip if already loaded or no URL
if (_modDatabaseLogo is not null || string.IsNullOrWhiteSpace(logoUrl)) 
    return;
```

#### 8b. Try Load from Image Cache ⭐

**Function**: `ModImageCacheService.TryGetCachedImageAsync()`

**Code Location**: `ModImageCacheService.cs` lines 22-50

**What Happens**:
1. **Generate cache file path**:
   - Cache directory: `%AppData%\VintageStoryModManager\Cache\ModDatabaseImages\`
   - Filename: SHA256 hash of the URL + original extension
   - Example: `aB3dF7gH9jK2mN5pQ8r.png`

2. **Acquire file lock** (prevents race conditions)
   - Uses `SemaphoreSlim` per file path
   - Thread-safe cache access

3. **Read cached bytes** from disk:
   ```csharp
   return await File.ReadAllBytesAsync(cachePath, cancellationToken)
       .ConfigureAwait(false);
   ```

4. **Return bytes** or null if file doesn't exist

**Cache Path Generation**: `ModImageCacheService.GetCachePath()`
**Code Location**: `ModImageCacheService.cs` lines 141-150

**Hash Computation**:
```csharp
private static string ComputeUrlHash(string url)
{
    var bytes = Encoding.UTF8.GetBytes(url);
    var hashBytes = SHA256.HashData(bytes);
    var base64 = Convert.ToBase64String(hashBytes);
    var urlSafe = base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    return urlSafe.Length > 32 ? urlSafe.Substring(0, 32) : urlSafe;
}
```

---

#### 8c. If Cache Hit: Create Image from Bytes

**Back to**: `ModListItemViewModel.LoadModDatabaseLogoAsync()`

**Code Location**: `ModListItemViewModel.cs` lines 1155-1164

```csharp
if (cachedBytes is { Length: > 0 })
{
    cancellationToken.ThrowIfCancellationRequested();
    var cachedImage = CreateBitmapFromBytes(cachedBytes, uri);
    if (cachedImage is not null)
    {
        await UpdateLogoOnDispatcherAsync(cachedImage, logoUrl, "cache", cancellationToken)
            .ConfigureAwait(false);
    }
    return; // Done! Image loaded from cache
}
```

**Image Creation**: `ModListItemViewModel.CreateBitmapFromBytes()`

**What Happens**:
1. Creates `MemoryStream` from byte array
2. Creates WPF `BitmapImage` object
3. Sets `CacheOption = BitmapCacheOption.OnLoad` (loads immediately)
4. Sets `CreateOptions = BitmapCreateOptions.IgnoreColorProfile`
5. Freezes the bitmap for thread safety
6. Returns the `ImageSource`

---

#### 8d. If Cache Miss: Download from Network

**Code Location**: `ModListItemViewModel.cs` lines 1168-1199

**What Happens**:
1. **Check internet availability**:
   ```csharp
   if (InternetAccessManager.IsInternetAccessDisabled) return;
   ```

2. **Make HTTP request**:
   ```csharp
   using var request = new HttpRequestMessage(HttpMethod.Get, uri);
   using var response = await HttpClient
       .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
       .ConfigureAwait(false);
   ```

3. **Read image bytes**:
   ```csharp
   var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken)
       .ConfigureAwait(false);
   ```

4. **Store in cache for future use**:
   ```csharp
   await ModImageCacheService.StoreImageAsync(logoUrl, payload, cancellationToken)
       .ConfigureAwait(false);
   ```

5. **Create and display image**:
   ```csharp
   var image = CreateBitmapFromBytes(payload, uri);
   if (image is not null)
   {
       await UpdateLogoOnDispatcherAsync(image, logoUrl, "network", cancellationToken)
           .ConfigureAwait(false);
   }
   ```

**Cache Storage**: `ModImageCacheService.StoreImageAsync()`

**Code Location**: `ModImageCacheService.cs` lines 58-121

**What Happens**:
1. **Validate inputs** (URL and bytes not empty)
2. **Create cache directory** if it doesn't exist
3. **Acquire file lock** for thread safety
4. **Write to temporary file** first:
   ```csharp
   var tempPath = cachePath + ".tmp";
   await File.WriteAllBytesAsync(tempPath, imageBytes, cancellationToken);
   ```
5. **Atomic move/replace** to final location:
   ```csharp
   File.Move(tempPath, cachePath, overwrite: true);
   ```
   Or use `File.Replace()` as fallback

---

### Step 9: Update UI on Dispatcher Thread

**Function**: `ModListItemViewModel.UpdateLogoOnDispatcherAsync()`

**Code Location**: `ModListItemViewModel.cs` lines 1211-1227

**What Happens**:
1. **Marshal to UI thread** using `InvokeOnDispatcherAsync()`
2. **Validate state** - ensure logo is still null and URL hasn't changed
3. **Update properties**:
   ```csharp
   _modDatabaseLogo = image;
   OnPropertyChanged(nameof(ModDatabasePreviewImage));
   OnPropertyChanged(nameof(HasModDatabasePreviewImage));
   ```

4. **Log success**:
   ```csharp
   LogDebug($"Loaded database logo from {source}. URL='{FormatValue(expectedUrl)}'.");
   ```

**Key Point**: The `OnPropertyChanged` notifications trigger WPF data binding updates!

---

### Step 10: WPF Binding Updates Display

**XAML Location**: `MainWindow.xaml`

**For DataGrid View** (lines 561-591):
```xml
<DataGridTemplateColumn x:Name="IconColumn" Header="Icon" Width="76">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <Border Width="64" Height="64">
                <Image Source="{Binding Icon}" Stretch="Uniform" />
            </Border>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

**For ModDB Design View** (ListView with cards) (lines 1617-1664):
```xml
<Border Width="240" Height="156">
    <Border.Background>
        <ImageBrush ImageSource="{Binding ModDatabasePreviewImage}" 
                    Stretch="UniformToFill" />
    </Border.Background>
</Border>
```

**Binding Property**: `ModListItemViewModel.ModDatabasePreviewImage`

**Code Location**: `ModListItemViewModel.cs` line 288

```csharp
public ImageSource? ModDatabasePreviewImage => Icon ?? _modDatabaseLogo;
```

**Fallback Logic**:
1. First tries to use `Icon` (from mod's own icon bytes if available)
2. Falls back to `_modDatabaseLogo` (downloaded from database)
3. Returns null if neither is available (shows default/blank)

---

## Summary of Complete Flow

```
User Action (Search/Browse Tab)
    ↓
MainViewModel.TriggerModDatabaseSearchAsync()
    ↓
MainViewModel.RunModDatabaseSearchAsync()
    ├─ ModDatabaseService.SearchModsWithCacheAsync()
    │   ├─ Check ModDatabaseSearchCacheService (5 min cache)
    │   ├─ If miss: HTTP GET to mods.vintagestory.at/api/mods
    │   └─ Parse JSON → ModDatabaseSearchResult[] (includes LogoUrl)
    │
    ├─ Create ModEntry[] from results
    ├─ Create ModListItemViewModel[] (stores LogoUrl)
    │
    ├─ Load cached ModDatabaseInfo
    │   └─ ModDatabaseService.TryLoadCachedDatabaseInfoAsync()
    │       └─ ModDatabaseCacheService (2 hour cache)
    │
    ├─ Populate from network
    │   └─ ModDatabaseService.PopulateModDatabaseInfoAsync()
    │       ├─ HTTP GET to mods.vintagestory.at/api/mod/{id}
    │       └─ Update ModListItemViewModel.UpdateDatabaseInfo()
    │
    └─ MainViewModel.LoadModDatabaseLogosAsync()
        └─ For each ModListItemViewModel:
            └─ ModListItemViewModel.LoadModDatabaseLogoAsync()
                ├─ ModImageCacheService.TryGetCachedImageAsync()
                │   ├─ Check cache: %AppData%\...\Cache\ModDatabaseImages\{hash}.png
                │   └─ If found: Return byte[]
                │
                ├─ If cache miss:
                │   ├─ HTTP GET image from LogoUrl
                │   └─ ModImageCacheService.StoreImageAsync()
                │       └─ Write to cache file
                │
                ├─ CreateBitmapFromBytes() → WPF BitmapImage
                │
                └─ UpdateLogoOnDispatcherAsync()
                    └─ UI Thread:
                        ├─ Set _modDatabaseLogo = image
                        ├─ OnPropertyChanged("ModDatabasePreviewImage")
                        └─ WPF Binding Updates!
                            └─ MainWindow.xaml
                                └─ Image.Source={Binding ModDatabasePreviewImage}
                                    └─ Image appears in UI! ✓
```

---

## Cache Locations

### Image Cache
- **Directory**: `%AppData%\VintageStoryModManager\Cache\ModDatabaseImages\`
- **File Format**: `{SHA256Hash}.{extension}`
- **Example**: `aB3dF7gH9jK2mN5pQ8r.png`
- **Expiry**: No automatic expiry (cleared manually via "Clear cache" menu)

### Database Metadata Cache
- **Directory**: `%AppData%\VintageStoryModManager\Cache\ModDatabase\`
- **File Format**: JSON files per mod
- **Expiry**: 2 hours (hard) / 5 minutes (soft for UI refreshes)

### Search Results Cache
- **Storage**: In-memory only
- **Expiry**: 5 minutes
- **Purpose**: Avoid re-querying during pagination/filtering

---

## Potential Issues and Logic Faults Identified

### 1. Race Condition on Logo URL Change
**Location**: `ModListItemViewModel.UpdateDatabaseInfo()` and `LoadModDatabaseLogoAsync()`

**Issue**: If database info is updated (changing LogoUrl) while an image is being loaded, there's a window where:
- Old image could be assigned after URL changed
- Check at line 1217: `string.Equals(_modDatabaseLogoUrl, expectedUrl)` mitigates this

**Severity**: Low - properly handled with URL validation

---

### 2. No Retry Logic on Download Failure
**Location**: `ModListItemViewModel.LoadModDatabaseLogoAsync()` lines 1168-1208

**Issue**: If HTTP request fails (network error, 404, etc.), the image is never loaded and no retry is attempted

**Current Behavior**: Silent failure - mod shows with no image

**Potential Fix**: Could implement retry with exponential backoff

**Severity**: Low - acceptable for non-critical images

---

### 3. Memory Pressure from Parallel Downloads
**Location**: `MainViewModel.LoadModDatabaseLogosAsync()` line 3766

**Issue**: Loads ALL visible mod images in parallel with `Task.WhenAll()`
- For 50 mods, creates 50 simultaneous HTTP connections
- Each image could be several MB
- Could cause memory spikes or connection throttling

**Current Mitigation**: None - relies on HttpClient's connection pooling

**Potential Fix**: Add semaphore to limit concurrent downloads (e.g., max 6 at once)

**Severity**: Medium - could impact performance on slow connections

---

### 4. Cache Never Expires
**Location**: `ModImageCacheService` - no expiry logic

**Issue**: Images are cached forever until manually cleared
- Old mod images may become stale if developer updates them
- Cache can grow unbounded

**Current Behavior**: User must manually "Clear cache" from menu

**Potential Fix**: Add timestamp metadata and auto-expire after X days

**Severity**: Low - acceptable for relatively static image assets

---

### 5. Frozen Bitmap Thread Safety Assumption
**Location**: `ModListItemViewModel.CreateBitmapFromBytes()`

**Issue**: Assumes `bitmap.Freeze()` is always successful
- If freeze fails, bitmap may not be thread-safe for cross-thread use

**Current Handling**: Catches exceptions but logs only

**Severity**: Very Low - freeze rarely fails in practice

---

### 6. Cache File Corruption Handling
**Location**: `ModImageCacheService.TryGetCachedImageAsync()`

**Issue**: If cache file is corrupted (partial write, disk error), read may fail
- Returns null, forcing re-download
- Corrupted file remains in cache

**Current Behavior**: Silent failure and re-download

**Potential Fix**: Validate file integrity or delete on read error

**Severity**: Low - rare occurrence

---

### 7. No Placeholder Image While Loading
**Location**: XAML bindings

**Issue**: While image is loading, binding shows nothing (null)
- No visual feedback that loading is in progress
- User sees blank space

**Current Behavior**: Blank until loaded

**Potential Fix**: Use value converter with loading placeholder image

**Severity**: Low - cosmetic issue only

---

## Performance Characteristics

### Best Case (All Cached):
1. Cache check: ~1-5ms per image
2. Disk read: ~5-20ms per image
3. BitmapImage creation: ~10-50ms per image
4. **Total per image**: ~16-75ms
5. **For 50 mods**: ~0.8-3.75 seconds (parallelized)

### Worst Case (All Network):
1. Cache check: ~1-5ms per image
2. HTTP request: ~500-2000ms per image
3. Download: ~100-1000ms per image (depends on image size/connection)
4. Cache write: ~10-50ms per image
5. BitmapImage creation: ~10-50ms per image
6. **Total per image**: ~621-3105ms
7. **For 50 mods**: ~31-155 seconds (parallelized, limited by connection pool)

### Typical Case (Mix):
- Assuming 70% cache hit rate
- **Average**: ~5-20 seconds for 50 mods

---

## Conclusion

The mod image loading system is **well-architected** with:
✓ Efficient disk caching
✓ Proper thread marshaling for WPF
✓ Race condition protection
✓ Graceful failure handling

Minor improvements could be made for:
- Concurrent download limiting
- Cache expiry strategy
- Better user feedback during loading
- Retry logic on network failures

Overall, the flow is **production-ready** and handles the typical use cases effectively.

---

**Document Version**: 1.0  
**Last Updated**: 2025-12-02  
**Reviewed Code**: Simple VS Manager v1.4.0
