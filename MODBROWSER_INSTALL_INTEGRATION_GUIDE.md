# ModBrowserView Install Integration Guide

## Quick Reference

**Goal:** Connect new ModBrowserView install button to use the exact same functions as the old mod database install flow.

**Strategy:** Create data adapters to convert API models → ViewModels, then call existing install functions.

**Key Files to Modify:**
- `MainWindow.xaml.cs` - Add adapter methods and refactor `InstallModFromBrowserAsync`
- `ModListItemViewModel.cs` - May need to add constructor/factory method

**Functions to Reuse (DO NOT duplicate):**
- `SelectReleaseForInstall()` - Line 5361
- `TryGetInstallTargetPath()` - Line 7524
- `CreateAutomaticBackupAsync()`
- `ModUpdateService.UpdateAsync()`

**Functions to Remove (duplicates):**
- `SelectReleaseForBrowserInstall()` - Line 2624
- `TryGetInstallTargetPathForBrowserMod()` - Line 2635

---

## Overview

This guide explains how to integrate the **new ModBrowserView** with the **existing mod installation flow** used by the old mod database browser. The goal is to achieve **exact same functionality** - when the install button is clicked in the new ModBrowserView, it should trigger the exact same functions and use the same variables as the old mod database install button.

## Current State Analysis

### Old Mod Database Install Flow (Target to Match)

**Entry Point:** `MainWindow.xaml.cs` - Line 5095  
**Method:** `InstallModButton_OnClick(object sender, RoutedEventArgs e)`

**Key Characteristics:**
- Uses `ModListItemViewModel` as the data context from the button sender
- Calls `SelectReleaseForInstall(ModListItemViewModel)` to choose the release
- Calls `TryGetInstallTargetPath(ModListItemViewModel, ModReleaseInfo, ...)` to get target path
- Creates `ModUpdateDescriptor` with these specific fields:
  - `ModId` (string)
  - `DisplayName` (string)
  - `DownloadUri` (Uri)
  - `TargetPath` (string, .zip file)
  - `TargetIsDirectory` (bool, always false)
  - `ReleaseFileName` (string)
  - `ReleaseVersion` (string)
  - `InstalledVersion` (string or null)

### New ModBrowser Install Flow (Current Implementation)

**Entry Point:** `MainWindow.xaml.cs` - Line 2521  
**Method:** `InstallModFromBrowserAsync(DownloadableMod mod)`

**Key Differences:**
- Uses `DownloadableMod` model instead of `ModListItemViewModel`
- Has its own separate helper methods:
  - `SelectReleaseForBrowserInstall(DownloadableMod)` - returns `DownloadableModRelease`
  - `TryGetInstallTargetPathForBrowserMod(DownloadableMod, DownloadableModRelease, ...)` 
- Uses `MainFile` directly as the download URL provided by the API
- Uses different property names from the API models

## Problem Statement

The current ModBrowser install flow uses **different data structures** (`DownloadableMod` and `DownloadableModRelease`) than the old flow (`ModListItemViewModel` and `ModReleaseInfo`). This creates code duplication and inconsistency.

## Solution: Unified Installation Flow

The solution is to **adapt the ModBrowser data** to work with the **existing old install functions**. We need to create an adapter/bridge that transforms `DownloadableMod` data into the format expected by the old install flow.

---

## Implementation Steps

### Step 1: Create Data Adapter Methods

Create new methods in `MainWindow.xaml.cs` that convert ModBrowser data structures to the old format.

#### 1.1: Convert DownloadableMod to ModListItemViewModel

**Purpose:** Transform the API's `DownloadableMod` into a `ModListItemViewModel` that can be used with the existing install functions.

**Location:** Add to `MainWindow.xaml.cs`

```csharp
/// <summary>
/// Converts a DownloadableMod from the API to a ModListItemViewModel
/// for use with the standard installation flow.
/// </summary>
private ModListItemViewModel ConvertToModListItemViewModel(DownloadableMod mod)
{
    // Create a new ModListItemViewModel
    var viewModel = new ModListItemViewModel
    {
        // Map ModId - API uses int, old system uses string
        ModId = mod.ModIdStr ?? mod.ModId.ToString(),
        
        // Map display name
        DisplayName = mod.Name,
        
        // Map other properties as needed
        Author = mod.Author,
        Version = null, // Not installed yet, so no installed version
        
        // Set HasDownloadableRelease based on releases availability
        // Note: This may require accessing the internal setter or using reflection
        // depending on how the property is implemented
    };
    
    // Convert releases to ModReleaseInfo format
    if (mod.Releases != null && mod.Releases.Count > 0)
    {
        var releases = mod.Releases
            .Select(r => ConvertToModReleaseInfo(r))
            .Where(r => r != null)
            .Cast<ModReleaseInfo>()
            .ToList();
            
        // Set the releases on the view model
        // This may require calling a method or setting a property
        // depending on the ModListItemViewModel implementation
        viewModel.SetReleases(releases); // Or however releases are set
    }
    
    return viewModel;
}
```

#### 1.2: Convert DownloadableModRelease to ModReleaseInfo

**Purpose:** Transform the API's `DownloadableModRelease` into a `ModReleaseInfo` for use with existing functions.

**Location:** Add to `MainWindow.xaml.cs`

```csharp
/// <summary>
/// Converts a DownloadableModRelease from the API to a ModReleaseInfo
/// for use with the standard installation flow.
/// </summary>
private ModReleaseInfo? ConvertToModReleaseInfo(DownloadableModRelease release)
{
    if (string.IsNullOrWhiteSpace(release.MainFile))
        return null;
        
    // MainFile already contains the full download URL
    var downloadUri = new Uri(release.MainFile);
    
    return new ModReleaseInfo
    {
        DownloadUri = downloadUri,
        FileName = release.Filename,
        Version = release.ModVersion,
        
        // Map compatibility information if available
        // This may require checking game version compatibility
        IsCompatibleWithInstalledGame = CheckCompatibility(release),
        
        // Add any other required properties
    };
}

/// <summary>
/// Checks if a release is compatible with the installed game version.
/// </summary>
private bool CheckCompatibility(DownloadableModRelease release)
{
    // Implement compatibility check logic
    // This may require comparing release.Tags with the installed game version
    // For now, return true as a safe default
    return true;
}
```

### Step 2: Refactor InstallModFromBrowserAsync

**Purpose:** Change the ModBrowser install flow to use the **exact same functions** as the old install flow.

**Current Implementation:** Lines 2521-2621 in `MainWindow.xaml.cs`

**New Implementation:**

```csharp
private async Task InstallModFromBrowserAsync(DownloadableMod mod)
{
    // Step 1: Convert the DownloadableMod to ModListItemViewModel
    // This allows us to use all the existing install functions
    var modViewModel = ConvertToModListItemViewModel(mod);
    
    // Step 2: Use the EXACT SAME validation logic as the old flow
    if (_isModUpdateInProgress) return;

    if (!modViewModel.HasDownloadableRelease)
    {
        WpfMessageBox.Show("No downloadable releases are available for this mod.",
            "Simple VS Manager",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return;
    }

    // Step 3: Use the SAME SelectReleaseForInstall function (Line 5361)
    var release = SelectReleaseForInstall(modViewModel);
    if (release is null)
    {
        WpfMessageBox.Show("No downloadable releases are available for this mod.",
            "Simple VS Manager",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return;
    }

    // Step 4: Use the SAME TryGetInstallTargetPath function (Line 7524)
    if (!TryGetInstallTargetPath(modViewModel, release, out var targetPath, out var errorMessage))
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
            WpfMessageBox.Show(errorMessage!,
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

        return;
    }

    // Step 5: Use the SAME backup function
    await CreateAutomaticBackupAsync("ModsUpdated").ConfigureAwait(true);

    // Step 6: Set the same flag
    _isModUpdateInProgress = true;
    UpdateSelectedModButtons();

    try
    {
        // Step 7: Create the SAME ModUpdateDescriptor structure
        var descriptor = new ModUpdateDescriptor(
            modViewModel.ModId,                 // Use converted ModId (string)
            modViewModel.DisplayName,           // Use converted DisplayName
            release.DownloadUri,                // From converted ModReleaseInfo
            targetPath,                         // From TryGetInstallTargetPath
            false,                              // TargetIsDirectory = false (zip-only)
            release.FileName,                   // From converted ModReleaseInfo
            release.Version,                    // From converted ModReleaseInfo
            modViewModel.Version);              // Installed version (null for new install)

        // Step 8: Use the SAME progress reporter
        var progress = new Progress<ModUpdateProgress>(p =>
            _viewModel?.ReportStatus($"{modViewModel.DisplayName}: {p.Message}"));

        // Step 9: Call the SAME ModUpdateService.UpdateAsync
        var result = await _modUpdateService
            .UpdateAsync(descriptor, _userConfiguration.CacheAllVersionsLocally, progress)
            .ConfigureAwait(true);

        // Step 10: Use the SAME error handling
        if (!result.Success)
        {
            var message = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? "The installation failed."
                : result.ErrorMessage!;
            _viewModel?.ReportStatus($"Failed to install {modViewModel.DisplayName}: {message}", true);
            WpfMessageBox.Show($"Failed to install {modViewModel.DisplayName}:{Environment.NewLine}{message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        // Step 11: Use the SAME success reporting
        var versionText = string.IsNullOrWhiteSpace(release.Version) ? string.Empty : $" {release.Version}";
        _viewModel?.ReportStatus($"Installed {modViewModel.DisplayName}{versionText}.");
        _modActivityLoggingService.LogModInstall(modViewModel.DisplayName ?? modViewModel.ModId ?? "Unknown", release.Version);

        // Step 12: Use the SAME refresh function
        await RefreshModsAsync().ConfigureAwait(true);

        // Step 13: ModBrowser-specific cleanup
        // Update the ModBrowserViewModel to mark as installed and remove from search
        AddModToInstalledAndRemoveFromSearch(mod.ModId);
        
        // Note: We don't need RemoveFromSelection since ModBrowser doesn't use selection
        // Note: We don't need _viewModel.RemoveSearchResult since ModBrowser has its own list
    }
    catch (OperationCanceledException)
    {
        _viewModel?.ReportStatus("Installation cancelled.");
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
    {
        _modActivityLoggingService.LogError($"Failed to install {modViewModel.DisplayName}", ex);
        _viewModel?.ReportStatus($"Failed to install {modViewModel.DisplayName}: {ex.Message}", true);
        WpfMessageBox.Show($"Failed to install {modViewModel.DisplayName}:{Environment.NewLine}{ex.Message}",
            "Simple VS Manager",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
    finally
    {
        // Step 14: Use the SAME cleanup
        _isModUpdateInProgress = false;
        UpdateSelectedModButtons();
    }
}
```

### Step 3: Remove Duplicate Helper Methods

**Purpose:** Eliminate code duplication by removing ModBrowser-specific helper methods.

**Methods to Remove:**
1. `SelectReleaseForBrowserInstall` (Line 2624) - **DELETE THIS**
2. `TryGetInstallTargetPathForBrowserMod` (Line 2635) - **DELETE THIS**

These are no longer needed because we now use:
- `SelectReleaseForInstall` (Line 5361) - The original function
- `TryGetInstallTargetPath` (Line 7524) - The original function

### Step 4: Handle ModListItemViewModel Implementation Details

**Challenge:** The `ModListItemViewModel` class may have private setters or restricted access.

**Solution Options:**

#### Option A: Add a Constructor (Recommended)
Add a new constructor to `ModListItemViewModel` that accepts the necessary data:

```csharp
public ModListItemViewModel(
    string modId,
    string displayName,
    string author,
    List<ModReleaseInfo> releases)
{
    ModId = modId;
    DisplayName = displayName;
    Author = author;
    // Set releases and compute HasDownloadableRelease
    _releases = releases;
    HasDownloadableRelease = releases.Count > 0;
    // ... set other properties
}
```

#### Option B: Add a Static Factory Method
Add a static factory method to create instances from API data:

```csharp
public static ModListItemViewModel FromDownloadableMod(DownloadableMod mod)
{
    // Create and configure the view model
    // This method has access to private setters
}
```

#### Option C: Add Public Setter Methods
Add methods to set the releases after construction:

```csharp
public void SetReleasesFromApi(List<ModReleaseInfo> releases)
{
    _releases = releases;
    HasDownloadableRelease = releases.Count > 0;
    OnPropertyChanged(nameof(HasDownloadableRelease));
}
```

**Recommendation:** Use **Option A or B** to maintain encapsulation.

---

## Key Differences to Handle

### 1. Data Type Conversions

| Old System | New API | Conversion |
|------------|---------|------------|
| `ModListItemViewModel` | `DownloadableMod` | Use adapter method |
| `ModReleaseInfo` | `DownloadableModRelease` | Use adapter method |
| `ModId` (string) | `ModId` (int) | `mod.ModIdStr ?? mod.ModId.ToString()` |
| `DownloadUri` (Uri) | `MainFile` (string) | `new Uri(release.MainFile)` |

### 2. Property Name Mappings

| Old System Property | New API Property |
|---------------------|------------------|
| `DisplayName` | `Name` |
| `Version` | `ModVersion` |
| `FileName` | `Filename` |
| `DownloadUri` | `MainFile` (already a full URL) |

### 3. Release Selection Logic

**Old System:** `SelectReleaseForInstall` (Line 5361)
- Returns latest compatible release
- Fallback to latest release

**Current ModBrowser:** `SelectReleaseForBrowserInstall` (Line 2624)
- Only returns latest release by date

**Solution:** Use the **old system's logic** as it's more sophisticated and handles compatibility properly.

### 4. Compatibility Checking

The old system has better compatibility checking logic. When converting `DownloadableModRelease` to `ModReleaseInfo`, we need to:

1. Check the release's `Tags` property against the installed game version
2. Set `IsCompatibleWithInstalledGame` property accordingly
3. Populate `LatestRelease` and `LatestCompatibleRelease` properties

---

## Testing the Integration

### Test Cases

1. **Fresh Install**
   - Select a mod that's not installed
   - Click install button
   - Verify it uses `TryGetInstallTargetPath` (check debug logs)
   - Verify it calls `ModUpdateService.UpdateAsync`
   - Verify the mod appears in the installed mods list

2. **Already Installed Mod**
   - Verify the install button is hidden for installed mods
   - Verify the `InstalledMods` collection is synced properly

3. **No Releases Available**
   - Test with a mod that has no releases
   - Verify the error message matches the old flow

4. **Download Failure**
   - Test with invalid download URL
   - Verify error handling matches old flow

5. **Cancellation**
   - Cancel installation mid-download
   - Verify cleanup happens properly

### Debug Points

Add debug logging to verify function calls:

```csharp
System.Diagnostics.Debug.WriteLine("[ModBrowser] Converting DownloadableMod to ModListItemViewModel");
System.Diagnostics.Debug.WriteLine($"[ModBrowser] Using SelectReleaseForInstall");
System.Diagnostics.Debug.WriteLine($"[ModBrowser] Using TryGetInstallTargetPath");
System.Diagnostics.Debug.WriteLine($"[ModBrowser] Target path: {targetPath}");
```

---

## Implementation Checklist

### Phase 1: Create Adapters
- [ ] Add `ConvertToModListItemViewModel` method
- [ ] Add `ConvertToModReleaseInfo` method
- [ ] Add compatibility check logic
- [ ] Test conversions with sample data

### Phase 2: Refactor Install Flow
- [ ] Update `InstallModFromBrowserAsync` to use adapter
- [ ] Replace browser-specific helper calls with old system calls
- [ ] Verify all property mappings are correct
- [ ] Test with a simple mod install

### Phase 3: Clean Up
- [ ] Remove `SelectReleaseForBrowserInstall`
- [ ] Remove `TryGetInstallTargetPathForBrowserMod`
- [ ] Verify no other code references these methods
- [ ] Run full build to check for errors

### Phase 4: Handle ModListItemViewModel
- [ ] Check current implementation of `ModListItemViewModel`
- [ ] Add constructor/factory method as needed
- [ ] Update adapter to use new construction method
- [ ] Test adapter with real data

### Phase 5: Testing
- [ ] Test fresh install
- [ ] Test with already installed mod
- [ ] Test with no releases
- [ ] Test error cases
- [ ] Test cancellation
- [ ] Verify ModBrowser list updates correctly

### Phase 6: Verification
- [ ] Compare old and new install flows side-by-side
- [ ] Verify identical function calls
- [ ] Verify identical variable usage
- [ ] Verify identical error handling
- [ ] Code review

---

## Common Issues and Solutions

### Issue 1: ModListItemViewModel Constructor Not Accessible

**Symptom:** Cannot create new instance of `ModListItemViewModel`

**Solution:** Add a public constructor or factory method as described in Step 4.

### Issue 2: HasDownloadableRelease Always False

**Symptom:** Install button doesn't appear even with valid releases

**Solution:** Ensure releases are properly set and `HasDownloadableRelease` is computed:

```csharp
viewModel.SetReleases(convertedReleases);
// This should internally set HasDownloadableRelease = (releases.Count > 0)
```

### Issue 3: Download URI Format Wrong

**Symptom:** Download fails with 404 error

**Solution:** Use the download URL provided by the API:

```csharp
var downloadUri = new Uri(release.MainFile);
```

### Issue 4: Target Path Conflicts

**Symptom:** File already exists errors

**Solution:** The `TryGetInstallTargetPath` function already handles this with `EnsureUniqueFilePath`. Verify it's being called correctly.

### Issue 5: ModBrowser List Not Updating

**Symptom:** Installed mod still appears in browser

**Solution:** Ensure `AddModToInstalledAndRemoveFromSearch(mod.ModId)` is called after successful install.

---

## Benefits of This Approach

1. **Code Reuse:** No duplication of install logic
2. **Consistency:** Same behavior for both old and new UI
3. **Maintainability:** Bug fixes apply to both systems
4. **Testing:** Only need to test one install flow
5. **Future-Proof:** Easy to add new install sources

---

## Alternative Approaches (Not Recommended)

### Alternative 1: Duplicate the Old Logic
Copy all the old install functions for ModBrowser use.

**Problem:** Code duplication, maintenance nightmare

### Alternative 2: Modify Old Functions to Accept Both Types
Add overloads or type checking to old functions.

**Problem:** Violates single responsibility, makes code complex

### Alternative 3: Create a Common Interface
Extract an interface that both data types implement.

**Problem:** Requires modifying external API models, not feasible

---

## Summary

The implementation strategy is to **adapt the new data structures to work with the existing functions** rather than duplicating logic. This is achieved through:

1. **Data Adapters:** Convert API models to internal ViewModels
2. **Function Reuse:** Call the exact same install functions
3. **Code Cleanup:** Remove duplicate helper methods

This ensures that the new ModBrowserView achieves **exact same functionality** as the old mod database browser while maintaining clean, maintainable code.

---

## References

- **Documentation:** `MOD_INSTALL_FLOW_DOCUMENTATION.md`
- **Old Install Flow:** `MainWindow.xaml.cs` Line 5095 (`InstallModButton_OnClick`)
- **Helper Functions:** `MainWindow.xaml.cs` Lines 5361, 7524
- **Current Browser Flow:** `MainWindow.xaml.cs` Line 2521 (`InstallModFromBrowserAsync`)
- **Install Service:** `Services/ModUpdateService.cs`

---

## Questions for Clarification

Before implementing, verify:

1. Can we modify `ModListItemViewModel` to add a constructor/factory method?
2. Is there existing compatibility checking logic we can reuse?
3. Are there any additional properties that need to be mapped?
4. Should we log the conversion for debugging purposes?
5. Are there any performance concerns with creating temporary ViewModels?
