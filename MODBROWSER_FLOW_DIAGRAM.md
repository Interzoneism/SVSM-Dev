# ModBrowserView Install Flow Diagram

## Current Flow (Before Integration)

```
┌─────────────────────────────────────────────────────────────────┐
│                    NEW MODBROWSER FLOW (Current)                 │
└─────────────────────────────────────────────────────────────────┘

User clicks Install Button in ModBrowserView
           ↓
ModBrowserViewModel.InstallModCommand.Execute(modId)
           ↓
MainWindow.InstallModFromBrowserAsync(DownloadableMod)
           ↓
    ┌──────────────────────────────────────┐
    │  Uses DIFFERENT helper methods       │
    ├──────────────────────────────────────┤
    │  • SelectReleaseForBrowserInstall()  │  ← Browser-specific
    │  • TryGetInstallTargetPathForBrowser │  ← Browser-specific
    │  • Manual URL construction           │  ← Duplicate logic
    └──────────────────────────────────────┘
           ↓
ModUpdateService.UpdateAsync(descriptor)
           ↓
     Mod Installed ✓


┌─────────────────────────────────────────────────────────────────┐
│                    OLD MOD DB FLOW (Target)                      │
└─────────────────────────────────────────────────────────────────┘

User clicks Install Button in Old Mod DB Card
           ↓
MainWindow.InstallModButton_OnClick(sender, e)
           ↓
    ┌──────────────────────────────────────┐
    │  Uses ORIGINAL helper methods        │
    ├──────────────────────────────────────┤
    │  • SelectReleaseForInstall()         │  ← Original
    │  • TryGetInstallTargetPath()         │  ← Original
    │  • Complete compatibility logic      │  ← Well-tested
    └──────────────────────────────────────┘
           ↓
ModUpdateService.UpdateAsync(descriptor)
           ↓
     Mod Installed ✓
```

**Problem:** Code duplication, inconsistent behavior, maintenance burden

---

## Target Flow (After Integration)

```
┌─────────────────────────────────────────────────────────────────┐
│                  UNIFIED INSTALLATION FLOW                       │
└─────────────────────────────────────────────────────────────────┘

User clicks Install Button in ModBrowserView
           ↓
ModBrowserViewModel.InstallModCommand.Execute(modId)
           ↓
MainWindow.InstallModFromBrowserAsync(DownloadableMod)
           ↓
    ┌──────────────────────────────────────────────────────┐
    │         DATA ADAPTER LAYER (NEW)                     │
    ├──────────────────────────────────────────────────────┤
    │  ConvertToModListItemViewModel(DownloadableMod)      │
    │          ↓                                           │
    │  Creates: ModListItemViewModel                       │
    │          ↓                                           │
    │  ConvertToModReleaseInfo(DownloadableModRelease)    │
    │          ↓                                           │
    │  Creates: List<ModReleaseInfo>                       │
    └──────────────────────────────────────────────────────┘
           ↓
    ┌──────────────────────────────────────┐
    │  Uses SAME helper methods            │
    ├──────────────────────────────────────┤
    │  • SelectReleaseForInstall()         │  ← Reused ✓
    │  • TryGetInstallTargetPath()         │  ← Reused ✓
    │  • Same compatibility logic          │  ← Reused ✓
    └──────────────────────────────────────┘
           ↓
ModUpdateService.UpdateAsync(descriptor)
           ↓
     Mod Installed ✓
           ↓
AddModToInstalledAndRemoveFromSearch(modId)
           ↓
   ModBrowser UI Updates ✓
```

**Benefits:** No duplication, consistent behavior, single source of truth

---

## Data Transformation Flow

```
API Model (DownloadableMod)                   Internal Model (ModListItemViewModel)
┌────────────────────────────┐               ┌────────────────────────────┐
│ ModId: int                 │               │ ModId: string              │
│ ModIdStr: string?          │               │ DisplayName: string        │
│ Name: string               │               │ Author: string             │
│ Author: string             │──────────────>│ Version: string?           │
│ Releases:                  │   ADAPTER     │ HasDownloadableRelease     │
│   List<DownloadableMod     │               │ LatestRelease              │
│         Release>           │               │ LatestCompatibleRelease    │
└────────────────────────────┘               └────────────────────────────┘
            │                                              ↑
            │ Releases                                     │
            ↓                                              │
┌────────────────────────────┐               ┌────────────────────────────┐
│ DownloadableModRelease     │               │ ModReleaseInfo             │
├────────────────────────────┤               ├────────────────────────────┤
│ MainFile: string           │               │ DownloadUri: Uri           │
│ Filename: string           │──────────────>│ FileName: string           │
│ ModVersion: string         │   ADAPTER     │ Version: string            │
│ Tags: List<string>         │               │ IsCompatibleWith...        │
└────────────────────────────┘               └────────────────────────────┘
```

---

## Function Call Comparison

### Before (Duplicated Code)

```
InstallModFromBrowserAsync
├─ SelectReleaseForBrowserInstall()         ← Browser-specific duplicate
├─ TryGetInstallTargetPathForBrowserMod()   ← Browser-specific duplicate
├─ Manual URL: release.MainFile
└─ ModUpdateService.UpdateAsync()           ← Same ✓

InstallModButton_OnClick
├─ SelectReleaseForInstall()                ← Original
├─ TryGetInstallTargetPath()                ← Original
└─ ModUpdateService.UpdateAsync()           ← Same ✓
```

**Issue:** Two versions of the same logic - bugs fixed in one don't apply to the other

### After (Unified Code)

```
InstallModFromBrowserAsync
├─ ConvertToModListItemViewModel()          ← NEW adapter
├─ SelectReleaseForInstall()                ← Reused ✓
├─ TryGetInstallTargetPath()                ← Reused ✓
└─ ModUpdateService.UpdateAsync()           ← Same ✓

InstallModButton_OnClick
├─ SelectReleaseForInstall()                ← Original ✓
├─ TryGetInstallTargetPath()                ← Original ✓
└─ ModUpdateService.UpdateAsync()           ← Same ✓
```

**Benefit:** Single implementation - bug fixes apply everywhere

---

## Implementation Phases

```
Phase 1: Create Adapters
┌────────────────────────────────────────────┐
│ Add ConvertToModListItemViewModel()       │
│ Add ConvertToModReleaseInfo()             │
│ Add CheckCompatibility()                  │
└────────────────────────────────────────────┘
            ↓

Phase 2: Refactor Install Flow
┌────────────────────────────────────────────┐
│ Update InstallModFromBrowserAsync()       │
│ Replace all browser-specific calls        │
│ Use adapter + original functions          │
└────────────────────────────────────────────┘
            ↓

Phase 3: Clean Up
┌────────────────────────────────────────────┐
│ Remove SelectReleaseForBrowserInstall()   │
│ Remove TryGetInstallTargetPathForBrowser() │
│ Verify no references remain               │
└────────────────────────────────────────────┘
            ↓

Phase 4: Handle ViewModels
┌────────────────────────────────────────────┐
│ Add constructor to ModListItemViewModel   │
│ OR add factory method                     │
│ Update adapter to use new method          │
└────────────────────────────────────────────┘
            ↓

Phase 5: Testing
┌────────────────────────────────────────────┐
│ Test fresh install                        │
│ Test already installed                    │
│ Test error cases                          │
│ Verify UI updates correctly               │
└────────────────────────────────────────────┘
            ↓

Phase 6: Verification
┌────────────────────────────────────────────┐
│ Compare flows side-by-side                │
│ Verify identical function calls           │
│ Code review                               │
│ Done! ✓                                   │
└────────────────────────────────────────────┘
```

---

## Key Decisions

### ✅ DO: Adapt data to fit existing functions
```
DownloadableMod → [ADAPTER] → ModListItemViewModel → SelectReleaseForInstall()
```

### ❌ DON'T: Duplicate the logic
```
DownloadableMod → SelectReleaseForBrowserInstall() ← Duplicate, bad!
```

### ✅ DO: Remove duplicate helper methods
```
Before:
- SelectReleaseForInstall()
- SelectReleaseForBrowserInstall()  ← DELETE THIS

After:
- SelectReleaseForInstall()  ← Keep only this
```

### ✅ DO: Use the same ModUpdateService call
```
var descriptor = new ModUpdateDescriptor(
    modViewModel.ModId,           ← From adapted data
    modViewModel.DisplayName,     ← From adapted data
    release.DownloadUri,          ← From adapted release
    targetPath,                   ← From original function
    false,                        ← Always false for zip
    release.FileName,             ← From adapted release
    release.Version,              ← From adapted release
    modViewModel.Version          ← From adapted data
);

await _modUpdateService.UpdateAsync(descriptor, ...);
```

---

## Success Criteria

✅ ModBrowser install uses `SelectReleaseForInstall()`  
✅ ModBrowser install uses `TryGetInstallTargetPath()`  
✅ ModBrowser install uses same `ModUpdateDescriptor` structure  
✅ Browser-specific helper methods are removed  
✅ Both flows produce identical installation results  
✅ Code duplication is eliminated  
✅ All tests pass  

---

## File Checklist

### Files to Modify
- [ ] `MainWindow.xaml.cs` - Add adapters, refactor InstallModFromBrowserAsync
- [ ] `ModListItemViewModel.cs` - Add constructor or factory method (if needed)

### Files to Review (no changes)
- `ModBrowserViewModel.cs` - Already has InstallModCallback setup ✓
- `ModBrowserView.xaml.cs` - Already calls InstallModCommand ✓
- `ModUpdateService.cs` - No changes needed ✓

### Files Created
- `MODBROWSER_INSTALL_INTEGRATION_GUIDE.md` - Complete implementation guide ✓
- `MODBROWSER_FLOW_DIAGRAM.md` - This visual reference ✓

---

## Quick Test Commands

```bash
# Build the solution
dotnet build -nologo -clp:Summary -warnaserror

# Run (if tests exist)
dotnet test --nologo --verbosity=minimal

# Search for duplicate methods (should return 0 after cleanup)
grep -n "SelectReleaseForBrowserInstall" MainWindow.xaml.cs
grep -n "TryGetInstallTargetPathForBrowserMod" MainWindow.xaml.cs
```
