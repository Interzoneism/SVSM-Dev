# ModBrowser Install Button - Testing Guide

## Overview
This guide explains how to test the fixes made to the ModBrowser install button functionality.

## Issues Fixed

### 1. Install Button Visibility
**Problem**: Install button was shown for all mods, even those already installed.

**Fix**: Updated `InstallButtonVisibilityConverter` to check the `InstalledMods` collection before showing the button. Now follows the same pattern as the favorite button.

**Expected Behavior**:
- Button only appears when hovering over an **uninstalled** mod
- Button is hidden for mods that are already installed
- The icon changes to a checkmark for installed mods

### 2. Debug Logging
**Problem**: No visibility into why the install button might not be working.

**Fix**: Added comprehensive debug logging throughout the install flow.

## Testing Steps

### Test 1: Button Visibility
1. Launch the application
2. Navigate to the "Mod Database" tab
3. Search for or scroll to find some mods
4. Hover over a mod card that is **not installed**
   - ✅ Install button should appear in the top-right corner
   - ✅ Icon should be a download icon
5. Hover over a mod card that **is installed**
   - ✅ Install button should NOT appear
   - ✅ Icon should show a checkmark instead (visible when hovering)

### Test 2: Install Functionality
1. Navigate to the "Mod Database" tab
2. Find an uninstalled mod and hover over it
3. Click the install button
4. Check the debug output window for these messages:
   ```
   [ModBrowserView] InstallButton_Click called
   [ModBrowserView] Got modId: <number>
   [ModBrowserView] Executing InstallModCommand with modId: <number>
   [ModBrowser] InstallModAsync called with modId: <number>
   [ModBrowser] Fetching mod details for modId: <number>
   [ModBrowser] Fetched mod details: <mod name>
   [ModBrowser] Calling install callback for mod: <mod name>
   [MainWindow] InstallModFromBrowserAsync called for mod: <mod name>
   [MainWindow] Converting DownloadableMod to ModListItemViewModel
   [MainWindow] Selecting release for install
   ```
5. ✅ Progress messages should appear in the status bar
6. ✅ Mod should install successfully
7. ✅ After installation, the mod should be removed from the search results or show a checkmark
8. ✅ The install button should no longer appear when hovering over the mod

### Test 3: Error Cases

#### If button does nothing:
Check debug output for these error messages:
- `[ModBrowserView] ViewModel is null!` - Indicates DataContext isn't set
- `[ModBrowserView] InstallModCommand is null!` - Indicates command wasn't generated
- `[ModBrowserView] Failed to get modId from Tag...` - Indicates Tag binding issue

#### If button doesn't appear at all:
1. Check if mod is already installed (button should be hidden)
2. Check debug output when hovering over a card
3. Verify InstalledMods collection is properly populated

#### If installation fails:
1. Check debug output for error messages from InstallModFromBrowserAsync
2. Look for exceptions in the output window
3. Verify internet access is enabled (not disabled in settings)

### Test 4: Installed Mods Recognition
1. Install a mod from the Mod Database
2. Wait for installation to complete
3. Refresh the mod list (if needed)
4. Go back to the Mod Database tab
5. Find the mod you just installed
6. ✅ The install button should not appear when hovering
7. ✅ The icon should show a checkmark
8. Use the "Installed" filter in the Mod Database
9. ✅ The installed mod should appear in the filtered list
10. Use the "Not Installed" filter
11. ✅ The installed mod should NOT appear in the filtered list

## Debug Output Reference

### Normal Flow
```
[ModBrowserView] InstallButton_Click called
[ModBrowserView] Got modId: 123
[ModBrowserView] Executing InstallModCommand with modId: 123
[ModBrowser] InstallModAsync called with modId: 123
[ModBrowser] Fetching mod details for modId: 123
[ModBrowser] Fetched mod details: ExampleMod
[ModBrowser] Calling install callback for mod: ExampleMod
[MainWindow] InstallModFromBrowserAsync called for mod: ExampleMod (ID: 123)
[MainWindow] Converting DownloadableMod to ModListItemViewModel
[MainWindow] Selecting release for install
```

### Error Cases
```
[ModBrowserView] ViewModel is null!
OR
[ModBrowserView] InstallModCommand is null!
OR
[ModBrowserView] Failed to get modId from Tag. Sender type: Button, Tag type: String
OR
[ModBrowser] Failed to fetch mod details for modId: 123
OR
[MainWindow] No downloadable releases available
OR
[MainWindow] SelectReleaseForInstall returned null
```

## Expected Results

After all tests:
- ✅ Install button only shows for uninstalled mods when hovering
- ✅ Install button successfully installs mods
- ✅ Installed mods are recognized and button is hidden
- ✅ Filtering by "Installed" and "Not Installed" works correctly
- ✅ Debug logging provides clear visibility into execution flow

## Troubleshooting

### Issue: Button doesn't appear at all
**Possible Causes**:
1. Mod is already installed (working as intended)
2. Visibility converter binding failed
3. InstalledMods collection not properly synced

**Debug Steps**:
1. Check if mod ID is in InstalledMods collection
2. Verify visibility converter is being called
3. Check binding errors in output window

### Issue: Button appears but does nothing
**Possible Causes**:
1. Command is null (not generated)
2. Command execution is failing silently
3. Callback not registered

**Debug Steps**:
1. Check for null command message in debug output
2. Verify InstallModAsync is being called
3. Check if callback is registered in InitializeModBrowserView

### Issue: Installation fails
**Possible Causes**:
1. No internet connection
2. Internet access disabled in settings
3. Invalid download URL
4. Mod has no releases

**Debug Steps**:
1. Check internet access setting
2. Verify mod has releases
3. Check debug output for specific error messages
4. Look for exceptions in output window

## Next Steps

Once testing is complete and issues are resolved:
1. Remove or reduce debug logging (keep critical error logging)
2. Add unit tests for converter logic
3. Consider adding user-facing error messages
4. Update documentation
