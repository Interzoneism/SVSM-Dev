# Mod Installation Integration Package

This package contains all the documentation and source code needed to integrate the Simple VS Manager's mod installation functionality into another mod browser.

## Package Contents

### 1. Documentation
- **MOD_INSTALL_FLOW_DOCUMENTATION.md** - Complete function list and flow documentation (ZIP files only)

### 2. Source Code Files
- **MainWindow_InstallFunctions.cs** - Extracted install-related functions from MainWindow with detailed comments

## Quick Start Guide

### Prerequisites
- `System.IO.Compression` namespace for ZIP handling
- Vintage Story data directory path
- Network access for downloading mods

## Important Constraints

### ZIP-Only Installation
This integration assumes **all mods are installed as ZIP files**. Do not use directory extraction features.

**Always ensure:**
- `ModUpdateDescriptor.TargetIsDirectory = false`
- `TargetPath` points to a .zip file (e.g., `/path/to/Mods/modname.zip`)
- Never call directory-related installation functions

### Required Data
For each mod installation, you need:
- **Mod ID** (unique identifier)
- **Download URI** (where to download the mod)
- **Target path** (where to save the .zip file)
- **Release version** (version being installed)
- **Release filename** (original filename of the mod)

### Optional Data
- **Installed version** (current version, for updates)
- **Existing path** (if updating and path changed)

## Installation Flow

```
User clicks Install
    ↓
Select latest release
    ↓
Determine target path (.zip file)
    ↓
Download mod (from cache or network)
    ↓
Validate ZIP contains modinfo.json
    ↓
Backup existing file (if updating)
    ↓
Copy ZIP to target location
    ↓
Cache download for future use
    ↓
Success!
```

## Error Handling

The `ModUpdateResult` indicates success or failure:
- `result.Success` - `true` if installation succeeded
- `result.ErrorMessage` - Error description if failed

Common error cases:
- Network errors during download
- Invalid ZIP file (missing modinfo.json)
- IO errors (permission denied, disk full, etc.)
- Internet access disabled

## Cache Management

The service automatically manages a cache of downloaded mods:
- **Before download:** Checks if mod version is already cached
- **After download:** Saves mod to cache for future use
- **Before update:** Caches old version for potential rollback

Cache benefits:
- Faster re-installation
- Offline installation support
- Version rollback capability

## Progress Reporting

The service reports progress through 5 stages:

1. **Downloading** - Downloading mod from network/cache
2. **Validating** - Checking ZIP file validity
3. **Preparing** - *(Not used in ZIP-only mode)*
4. **Replacing** - Installing/replacing mod file
5. **Completed** - Installation finished

Each stage provides a human-readable message for UI display.

## Integration Checklist

- [ ] Add reference to `System.IO.Compression`
- [ ] Implement progress reporting
- [ ] Get Vintage Story data directory path
- [ ] Create mod installation UI
- [ ] Call `ModUpdateService.UpdateAsync` with proper descriptor
- [ ] Handle success/failure results
- [ ] Refresh mod list after installation

## Additional Resources

### Required Helper Services (from Simple VS Manager)
The ModUpdateService depends on a few helper services that you need to implement:

1. **ModCacheLocator** - Locates and manages cached mod files
2. **InternetAccessManager** - Checks if internet access is enabled
3. **ModCacheService** - Higher-level cache operations


### Data Structures
The following types are used:
- `ModUpdateDescriptor` - Installation parameters (in ModUpdateService.cs)
- `ModUpdateResult` - Installation result (in ModUpdateService.cs)
- `ModUpdateProgress` - Progress information (in ModUpdateService.cs)
- `ModUpdateStage` - Stage enumeration (in ModUpdateService.cs)

### External Dependencies
- `ModReleaseInfo` - Information about a mod release (from your mod browser)
- `ModListItemViewModel` - View model for mod display (from your mod browser)

## Support

For questions or issues with integration:
1. Review the complete documentation in `MOD_INSTALL_FLOW_DOCUMENTATION.md`
2. Check the reference implementation in `MainWindow_InstallFunctions.cs`

