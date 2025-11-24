# Instructions for Next Agent - Avalonia Migration

## Mission
Complete the WPF to Avalonia migration that was started. The infrastructure is in place, you need to finish getting it to build and then convert the MainWindow UI.

## Current Situation

### What's Done ✅
- Avalonia packages installed and configured
- Basic app structure created (AppAvalonia.axaml, Program.cs, MainWindowAvalonia.axaml)
- Old WPF files excluded from build but kept for reference
- Compatibility layer (WpfStubs.cs) created for gradual migration
- Build errors reduced from 443 to 46 (89% done!)

### What's Needed ❌
**Immediate**: Fix 46 remaining build errors (should take <30 minutes)
**Then**: Start converting MainWindow UI from WPF to Avalonia

## Quick Start Guide

### Step 1: Fix Build Errors (~20-30 mins)

Open `/VintageStoryModManager/WpfCompatibility/WpfStubs.cs` and update:

1. **Dispatcher class** - Add missing method overloads:
```csharp
public class Dispatcher
{
    public void Invoke(Action callback) => callback();
    public void Invoke(Action callback, DispatcherPriority priority) => callback();
    public void BeginInvoke(Delegate method, DispatcherPriority priority, params object[] args) 
        => method.DynamicInvoke(args);
    public bool CheckAccess() => true;
    public Task InvokeAsync(Action callback) => Task.Run(callback);
    public Task<T> InvokeAsync<T>(Func<T> callback) => Task.Run(callback);
}
```

2. **StreamResourceInfo** - Add new class:
```csharp
public class StreamResourceInfo
{
    public System.IO.Stream? Stream { get; set; }
}

// Update Application.GetResourceStream:
public static StreamResourceInfo? GetResourceStream(Uri uri) 
    => new StreamResourceInfo { Stream = null };
```

3. **ICollectionView** - Add DeferRefresh:
```csharp
public interface ICollectionView : System.Collections.IEnumerable, System.Collections.Specialized.INotifyCollectionChanged
{
    // ... existing members ...
    IDisposable DeferRefresh();
}

// Add helper class in same file:
private class DeferHelper : IDisposable
{
    public void Dispose() { }
}
```

4. **Build and verify**:
```bash
cd /home/runner/work/ImprovedModMenu/ImprovedModMenu
dotnet build -nologo -clp:Summary
```

### Step 2: Test Application Launch

Once build succeeds:
```bash
dotnet run --project VintageStoryModManager/VintageStoryModManager.csproj
```

Expected: Window opens showing "Avalonia Migration In Progress..."

**IMPORTANT**: Take a screenshot of the window and include in PR!

### Step 3: Start MainWindow Conversion

The original WPF MainWindow is HUGE:
- 4,175 lines of XAML
- 13,251 lines of C# code-behind

**Strategy**: Convert in small sections, test after each.

#### Recommended Conversion Order:
1. **Window properties** (Title, Size, MinSize, etc.)
2. **Resource dictionaries** (converters, styles) 
3. **Main Grid layout**
4. **Toolbar/menu area**
5. **Mod list area** (most important)
6. **Search/filter area**
7. **Details panel**
8. **Status bar**

#### Reference Files:
- Original WPF: `/VintageStoryModManager/Views/MainWindow.xaml`
- Original code: `/VintageStoryModManager/Views/MainWindow.xaml.cs`
- Target Avalonia: `/VintageStoryModManager/Views/MainWindowAvalonia.axaml`
- Target code: `/VintageStoryModManager/Views/MainWindowAvalonia.axaml.cs`

## Important Notes

1. **Don't touch old WPF files** - They're excluded from build, keep as reference
2. **WpfStubs.cs is temporary** - These are just to make it compile, not functional
3. **Focus on structure first** - Get UI layout working before styling
4. **Use SukiUI components** - Where possible, replace WPF controls with SukiUI equivalents
5. **Test frequently** - Build and run after each major change

## Key WPF → Avalonia Conversions

### XAML Namespace:
```xml
<!-- WPF -->
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"

<!-- Avalonia -->
<Window xmlns="https://github.com/avaloniaui"
```

### Common Controls:
- `Window` → `Window` (similar)
- `Grid`, `StackPanel`, `DockPanel` → Same names, similar usage
- `TextBlock` → `TextBlock` (same)
- `TextBox` → `TextBox` (similar)
- `Button` → `Button` (same)
- `ListView` → `ListBox` or `ItemsRepeater`
- `DataGrid` → `DataGrid` (Avalonia has one)
- ModernWpf controls → SukiUI equivalents

### Binding:
```xml
<!-- WPF -->
<TextBlock Text="{Binding PropertyName}" />

<!-- Avalonia -->
<TextBlock Text="{Binding PropertyName}" />
<!-- Same! But use compiled bindings when possible -->
```

## Where to Find Help

1. **MIGRATION_PROGRESS.md** - Complete migration plan and notes
2. **Avalonia docs**: https://docs.avaloniaui.net/
3. **SukiUI docs**: Look at their GitHub samples
4. **Original WPF code** - All in the repo for reference

## Success Criteria

**Minimum for this session**:
- [ ] Build succeeds with 0 errors
- [ ] Application launches
- [ ] Window displays (even if just placeholder)
- [ ] Screenshot taken and committed

**Ideal if time permits**:
- [ ] Basic MainWindow layout converted
- [ ] At least one section of UI working (e.g., toolbar)
- [ ] Documented progress in MIGRATION_PROGRESS.md

## If You Get Stuck

1. **Build errors**: Add more stubs to WpfStubs.cs as needed
2. **Runtime errors**: May need to initialize Application.Current in Program.cs
3. **UI not showing**: Check XAML syntax, Avalonia is strict about namespaces
4. **Complex conversions**: Start simple, add complexity later

## Final Tips

- Commit frequently with clear messages
- Update MIGRATION_PROGRESS.md as you go
- Don't aim for perfection - working > perfect
- The goal is incremental progress
- Leave clear notes for the next agent

Good luck! You've got this! 🚀
