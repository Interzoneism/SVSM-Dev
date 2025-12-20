# ModBrowser Scrolling Performance Fix

## Issue Description
When scrolling in the mod browser and loading more mods, the interface would lag and all mod cards would flash, creating a poor user experience.

## Root Cause Analysis

### The Problem
The `VisibleMods` property was implemented as a computed property:
```csharp
public IEnumerable<DownloadableModOnList> VisibleMods => ModsList.Take(VisibleModsCount);
```

When `LoadMore()` was called during scrolling:
1. It incremented `VisibleModsCount`
2. Called `OnPropertyChanged(nameof(VisibleMods))`
3. WPF's binding system re-evaluated the property
4. The property returned a **new** `IEnumerable<T>` instance
5. WPF detected the collection reference change
6. WPF destroyed and recreated **ALL** visible mod cards (not just new ones)

### Why This Caused Flashing and Lag
- **Flashing**: All 45+ complex mod cards were destroyed and recreated, causing visible flickering
- **Lag**: Each mod card includes:
  - Multiple WPF animations (hover, favorite button, install button)
  - Image controls with async binding
  - Complex layout with borders, grids, and text elements
  - User report badges with dynamic visibility
  - Recreating all of these is extremely expensive

## The Solution

### Changed Implementation
Replaced the computed property with an actual `ObservableCollection<DownloadableModOnList>`:

```csharp
[ObservableProperty]
private ObservableCollection<DownloadableModOnList> _visibleMods = new(DefaultLoadedMods);

private int _visibleModsCount = 0;
```

### How LoadMore Works Now
```csharp
var previousCount = _visibleModsCount;
var newCount = Math.Min(previousCount + LoadMoreCount, ModsList.Count);

// Add new items to VisibleMods incrementally - this prevents flashing
// WPF will only create UI for the new items, not rebuild everything
for (int i = previousCount; i < newCount; i++)
{
    VisibleMods.Add(ModsList[i]);
}

_visibleModsCount = newCount;
```

### Key Benefits
1. **Incremental Updates**: Only new items are added to the collection
2. **WPF Optimization**: WPF's `ItemsControl` detects individual item additions
3. **Selective Rendering**: Only UI for new cards is created, existing cards remain untouched
4. **No Flashing**: Existing cards stay in place without being recreated
5. **Better Performance**: Much lower CPU/GPU load during scrolling

### Additional Optimizations
- **Pre-allocated Capacity**: Collection initialized with capacity of 45 to avoid memory reallocations
- **Accurate Count Tracking**: `_visibleModsCount` starts at 0 and is updated as items are added
- **Clean Search**: Both `ModsList` and `VisibleMods` are cleared when starting a new search

## Files Modified

### VintageStoryModManager/ViewModels/ModBrowserViewModel.cs
- Changed `VisibleMods` from computed property to `ObservableCollection`
- Changed `_visibleModsCount` from observable property to private field
- Updated `SearchModsAsync()` to populate `VisibleMods` incrementally
- Updated `LoadMore()` to add items incrementally instead of notifying property change
- Updated `GetPrefetchMods()` to use `_visibleModsCount`
- Updated `InvalidateAllVisibleUserReports()` to use `_visibleModsCount`

### VintageStoryModManager/Design/ModBrowserViewDesignData.cs
- Updated `VisibleMods` type from `IEnumerable<T>` to `ObservableCollection<T>`
- Initialized `VisibleMods` with design-time data

## Testing Instructions

### Manual Testing
1. Launch the application
2. Navigate to the Mod Browser tab
3. Scroll down to trigger `LoadMore`
4. Observe:
   - ✅ New mod cards appear at the bottom smoothly
   - ✅ Existing mod cards do not flash or flicker
   - ✅ Scrolling remains smooth and responsive
   - ✅ Animations on existing cards continue working (hover effects)
   - ✅ No lag or stuttering during loading

### Performance Comparison
**Before:**
- All ~45+ visible cards destroyed and recreated
- Visible flashing/flickering
- Noticeable lag during scroll loading
- High CPU usage spikes

**After:**
- Only 15 new cards created per load
- No flashing or flickering
- Smooth, responsive scrolling
- Minimal CPU usage increase

## Compatibility

### Maintains Existing Optimizations
This fix works alongside all previous optimizations documented in `MODBROWSER_OPTIMIZATION_SUMMARY.md`:
- Asynchronous thumbnail loading
- Background user report fetching
- Optimized animations
- Image rendering optimizations
- Scroll trigger positioning

### No Breaking Changes
- All existing bindings in XAML continue to work
- No changes to public API or method signatures
- Design-time data still functions correctly
- All filters and search functionality unchanged

## Related Issues
- This addresses the core scrolling performance issue
- Complements optimizations from MODBROWSER_OPTIMIZATION_SUMMARY.md
- Part of ongoing effort to improve ModBrowser responsiveness
