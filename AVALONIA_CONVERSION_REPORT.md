# WPF to Avalonia Conversion Report: MainWindow

## Executive Summary

This report provides a comprehensive analysis and strategic plan for converting the **Simple VS Manager** application from WPF to Avalonia UI. The focus is on the MainWindow, which is the largest and most complex component of the application.

### Current State Analysis

- **MainWindow.xaml**: 2,575 lines
- **MainWindow.xaml.cs**: 14,232 lines
- **UI Elements**: ~643 controls (MenuItems, Buttons, TextBlocks, DataGrids, etc.)
- **Framework Dependencies**: WPF, ModernWpfUI
- **Architecture**: MVVM with CommunityToolkit.Mvvm

---

## Part 1: Pre-Conversion Refactoring Strategy

To make the Avalonia conversion viable, the MainWindow must be decomposed into smaller, manageable components. This section outlines **what can be done beforehand** to split up the MainWindow.

### 1.1 Component Extraction Strategy

The MainWindow can be divided into the following logical components:

#### **Critical Sections to Extract (High Priority)**

1. **Menu System** (~450 lines)
   - File Menu
   - Mods Menu
   - Folders Menu
   - Save & Load Menu
   - Performance Menu
   - View Menu
   - Themes Menu
   - Developer Menu
   - Help Menu
   - **Target**: Extract to `Views/Components/MainMenu.xaml`

2. **Mod Info Panel** (~300 lines)
   - The draggable panel showing selected mod details
   - Currently positioned using Canvas
   - **Target**: Extract to `Views/Components/ModInfoPanel.xaml`

3. **Status Bar / Notification Area** (~150 lines)
   - Bottom status messages
   - Update notifications
   - Mod usage prompt indicators
   - **Target**: Extract to `Views/Components/StatusBar.xaml`

4. **Tab Control Content** (~1,200 lines)
   - **Installed Mods Tab** (DataGrid + filters)
   - **Mod Browser Tab** (Already separate: `ModBrowserView.xaml`)
   - **Modlists Tab** (Local + Cloud)
   - **Target**: Extract tabs to separate user controls

5. **Overlay Components** (~200 lines)
   - Modlist Installation Progress Overlay
   - Data Backup Progress Overlay
   - **Target**: Extract to `Views/Components/ProgressOverlay.xaml`

6. **Filter/Search Controls** (~250 lines)
   - Search boxes
   - Tag filters
   - Sort options
   - Side filter (Client/Server/Universal)
   - **Target**: Extract to `Views/Components/FilterBar.xaml`

#### **Component Extraction Benefits**

- **Testability**: Each component can be tested independently
- **Reusability**: Components can be reused in Avalonia version
- **Maintainability**: Smaller files are easier to understand and modify
- **Conversion**: Smaller chunks are easier to convert one at a time
- **Parallel Work**: Different developers can work on different components

### 1.2 Detailed Extraction Plan

#### **Step 1: Extract Menu System (Week 1)**

**Action Items:**
1. Create `Views/Components/MainMenu.xaml` and `MainMenu.xaml.cs`
2. Move all `<Menu>` elements from MainWindow
3. Create a `MainMenuViewModel` if needed, or use commands
4. Expose events or commands for menu actions
5. Update MainWindow to reference `<components:MainMenu />`

**Benefits:**
- Reduces MainWindow.xaml by ~450 lines (17.5%)
- Menu is self-contained and easier to convert
- Menu items can be tested independently

**Risks:**
- Menu items rely on MainWindow methods (requires command pattern)
- Resource dictionary references may need adjustment

**Code-behind Impact:**
- Menu event handlers (~2,000 lines) can stay in MainWindow.xaml.cs initially
- Later, migrate to commands in ViewModel

---

#### **Step 2: Extract Mod Info Panel (Week 1-2)**

**Action Items:**
1. Create `Views/Components/ModInfoPanel.xaml` and `ModInfoPanel.xaml.cs`
2. Move the draggable mod info border (lines ~2150-2475)
3. Create dependency properties for selected mod data
4. Implement drag behavior in the component's code-behind
5. Update MainWindow to host the component

**Benefits:**
- Reduces MainWindow.xaml by ~300 lines (11.6%)
- Isolates complex drag-and-drop logic
- Makes the panel reusable

**Risks:**
- Drag logic is tightly coupled to MainWindow (Canvas positioning)
- May need to refactor positioning logic

**Code-behind Impact:**
- Mod info panel drag methods (~200 lines) move to component
- Selected mod update logic remains in MainWindow initially

---

#### **Step 3: Extract Progress Overlays (Week 2)**

**Action Items:**
1. Create `Views/Components/ProgressOverlay.xaml`
2. Extract both overlay types (Modlist Install + Data Backup)
3. Create a reusable progress overlay component with customizable content
4. Use dependency properties for progress value and message
5. Update MainWindow to use the component twice

**Benefits:**
- Reduces MainWindow.xaml by ~200 lines (7.8%)
- Creates a reusable pattern for future overlays
- Simplifies MainWindow structure

**Risks:**
- Low risk - overlays are relatively self-contained

**Code-behind Impact:**
- Overlay control methods (~150 lines) may need minor adjustments

---

#### **Step 4: Extract Filter Bar (Week 2-3)**

**Action Items:**
1. Create `Views/Components/FilterBar.xaml` and `FilterBar.xaml.cs`
2. Move search boxes, tag filters, sort options
3. Expose filter changes via events or commands
4. Bind to MainViewModel's filter properties
5. Update MainWindow to reference the component

**Benefits:**
- Reduces MainWindow.xaml by ~250 lines (9.7%)
- Filter logic becomes testable
- Easier to add new filters

**Risks:**
- Filter logic is spread across ViewModel and MainWindow
- May require ViewModel refactoring

**Code-behind Impact:**
- Filter event handlers (~300 lines) can be refactored to ViewModel

---

#### **Step 5: Extract Tab Content (Week 3-4)**

**Action Items:**
1. **Installed Mods Tab**:
   - Create `Views/InstalledModsTab.xaml` (or use existing `InstalledModsView.xaml`)
   - Move DataGrid and related controls
   - Keep it as a separate user control

2. **Modlists Tab**:
   - Create `Views/ModlistsTab.xaml`
   - Extract both Local and Cloud sub-tabs
   - Create `LocalModlistsView.xaml` and `CloudModlistsView.xaml`

3. **Mod Browser Tab**:
   - Already extracted as `ModBrowserView.xaml` ✓

**Benefits:**
- Reduces MainWindow.xaml by ~1,200 lines (46.6%)
- Each tab is self-contained
- Easier to test individual tabs

**Risks:**
- Tabs share the same ViewModel (MainViewModel)
- Selection state management across tabs

**Code-behind Impact:**
- Tab-specific logic (~3,000 lines) can move to tab components
- Shared logic (selection, refresh) stays in MainWindow

---

#### **Step 6: Extract Status Bar (Week 4)**

**Action Items:**
1. Create `Views/Components/StatusBar.xaml` and `StatusBar.xaml.cs`
2. Move status log, update notifications, mod usage prompts
3. Bind to MainViewModel status properties
4. Update MainWindow to reference the component

**Benefits:**
- Reduces MainWindow.xaml by ~150 lines (5.8%)
- Status logic is isolated
- Easier to add new status types

**Risks:**
- Low risk - status bar is relatively simple

**Code-behind Impact:**
- Status update methods (~100 lines) may need minor adjustments

---

### 1.3 Post-Extraction State

After all extractions, MainWindow.xaml should be reduced to:

- **Original**: 2,575 lines
- **After Extraction**: ~525 lines (79.6% reduction)
- **Remaining Content**:
  - Window properties and configuration
  - Resource dictionaries (shared styles)
  - Root Grid layout
  - Component references (Menu, FilterBar, TabControl, StatusBar, etc.)
  - Mod Info Panel (positioned)
  - Overlays (positioned)

**MainWindow.xaml.cs** would be reduced from 14,232 lines to ~8,000-9,000 lines:
- Remove component-specific logic (~5,000 lines)
- Keep orchestration logic (tab management, selection, refresh)
- Keep service initialization and lifecycle management

---

## Part 2: Conversion Order and Steps

This section outlines **in what order** the conversion should proceed after pre-conversion refactoring.

### 2.1 Recommended Conversion Order

#### **Phase 1: Foundation (Weeks 1-2)**

1. **Setup Avalonia Project Structure**
   - Create new Avalonia project alongside WPF project
   - Configure solution to share ViewModels and Services
   - Set up Avalonia.Themes.Fluent (similar to ModernWpfUI)
   - Configure dependency injection (if needed)

2. **Migrate Shared Resources**
   - Convert color themes from WPF to Avalonia
   - Port custom styles (buttons, text blocks, etc.)
   - Create Avalonia equivalents for resource dictionaries
   - Test theme switching functionality

3. **Migrate ViewModels**
   - MainViewModel (mostly unchanged - uses CommunityToolkit.Mvvm)
   - ModBrowserViewModel
   - Other ViewModels
   - **Note**: CommunityToolkit.Mvvm works with Avalonia, so minimal changes needed

#### **Phase 2: Simple Components (Weeks 3-4)**

Convert the simplest extracted components first to build confidence:

1. **Status Bar** (simplest)
   - Convert XAML to Avalonia syntax
   - Update bindings (mostly identical)
   - Test in isolation

2. **Progress Overlays** (simple)
   - Convert overlays to Avalonia panels
   - Replace WPF ProgressBar with Avalonia ProgressBar
   - Test overlay visibility and animations

3. **Filter Bar** (medium complexity)
   - Convert search boxes and combo boxes
   - Update tag filter rendering
   - Test filter functionality

#### **Phase 3: Complex Components (Weeks 5-7)**

1. **Menu System** (complex)
   - Convert menu structure to Avalonia
   - Replace WPF MenuItem with Avalonia MenuItem
   - Update menu styling (Avalonia uses different styling approach)
   - Implement command bindings for menu actions
   - **Challenge**: ModernWpfUI styles don't exist in Avalonia

2. **Mod Info Panel** (complex)
   - Convert drag-and-drop logic to Avalonia
   - Replace Canvas positioning with Avalonia approach
   - Update layout and styling
   - **Challenge**: Avalonia drag/drop API differs from WPF

#### **Phase 4: Tab Content (Weeks 8-10)**

1. **Installed Mods Tab** (most complex)
   - Convert DataGrid to Avalonia DataGrid
   - Update column templates and styling
   - Implement row selection logic
   - Test sorting and filtering
   - **Challenge**: Avalonia DataGrid has different features than WPF DataGrid

2. **Modlists Tab** (complex)
   - Convert local and cloud modlist views
   - Update list rendering
   - Test cloud synchronization

3. **Mod Browser Tab** (complex)
   - Convert existing ModBrowserView.xaml
   - Update search and filter UI
   - Test mod installation from browser

#### **Phase 5: MainWindow Assembly (Weeks 11-12)**

1. **Assemble MainWindow**
   - Create MainWindow.axaml (Avalonia XAML)
   - Reference all converted components
   - Set up root grid layout
   - Configure window properties

2. **Window Lifecycle**
   - Convert window initialization logic
   - Update window size/position persistence
   - Handle window events (Loaded, Closing, etc.)

3. **Integration Testing**
   - Test all components working together
   - Verify tab switching
   - Test mod selection across views
   - Verify drag-and-drop, filtering, searching

#### **Phase 6: Platform-Specific Features (Weeks 13-14)**

1. **File Dialogs**
   - Replace WPF OpenFileDialog with Avalonia dialogs
   - Replace WPF SaveFileDialog
   - Replace WPF FolderBrowserDialog

2. **Clipboard Operations**
   - Update clipboard access for Avalonia
   - Test copy/paste functionality

3. **External Process Launching**
   - Verify game launch functionality
   - Test file explorer opening

4. **Platform Integration**
   - Test on Windows
   - Test on Linux (if cross-platform is desired)
   - Test on macOS (if cross-platform is desired)

---

### 2.2 Critical Conversion Challenges

#### **Challenge 1: ModernWpfUI → Avalonia Themes**

- **WPF**: Uses ModernWpfUI for modern styling
- **Avalonia**: Use Avalonia.Themes.Fluent or custom theme
- **Solution**: Recreate ModernWpfUI styles in Avalonia
- **Effort**: ~2-3 weeks

#### **Challenge 2: DataGrid Differences**

- **WPF DataGrid**: Rich feature set, complex styling
- **Avalonia DataGrid**: Different API, fewer built-in features
- **Solution**: May need custom columns or workarounds
- **Effort**: ~1-2 weeks per complex grid

#### **Challenge 3: Drag-and-Drop**

- **WPF**: DragDrop.DoDragDrop API
- **Avalonia**: Different drag/drop event model
- **Solution**: Rewrite drag logic using Avalonia patterns
- **Effort**: ~1 week for Mod Info Panel

#### **Challenge 4: Canvas Positioning**

- **WPF**: Canvas with attached properties (Canvas.Left, Canvas.Top)
- **Avalonia**: Canvas works similarly, but layout may differ
- **Solution**: Test and adjust positioning logic
- **Effort**: ~2-3 days

#### **Challenge 5: Window Chrome and Custom Styling**

- **WPF**: Easy to customize window chrome
- **Avalonia**: Different approach to window decoration
- **Solution**: Use Avalonia window styling patterns
- **Effort**: ~1 week

---

## Part 3: Code-Behind Migration Approach

This section outlines **what parts need to be converted** in the .cs file and **what can stay**.

### 3.1 Code-Behind Analysis (MainWindow.xaml.cs - 14,232 lines)

#### **Section Breakdown**

| Section | Lines | Purpose | Conversion Strategy |
|---------|-------|---------|---------------------|
| **Using statements** | ~70 | Imports | **CONVERT**: Update to Avalonia namespaces |
| **Constants** | ~90 | Configuration | **KEEP**: No changes needed |
| **Dependency Properties** | ~120 | WPF-specific properties | **CONVERT**: Use Avalonia dependency properties or regular properties |
| **Private Fields** | ~300 | State management | **KEEP**: Mostly unchanged |
| **Constructor** | ~70 | Initialization | **CONVERT**: Update control references |
| **Column Visibility** | ~200 | DataGrid configuration | **MIGRATE**: Move to ViewModel or component |
| **Window Lifecycle** | ~500 | Load/Close events | **CONVERT**: Update to Avalonia lifecycle |
| **Mod Info Panel Drag** | ~200 | Drag positioning | **CONVERT**: Update to Avalonia drag APIs |
| **Menu Handlers** | ~2,000 | Menu click events | **REFACTOR**: Convert to commands in ViewModel |
| **Path Selection** | ~800 | File/folder dialogs | **CONVERT**: Use Avalonia dialogs |
| **Preset/Modlist Loading** | ~3,000 | Business logic | **KEEP**: No UI-specific code |
| **Mod Selection** | ~1,500 | Selection management | **PARTIALLY CONVERT**: Update visual tree navigation |
| **Tab Management** | ~400 | Tab switching | **CONVERT**: Update tab control references |
| **Filtering/Sorting** | ~500 | DataGrid filtering | **MIGRATE**: Move to ViewModel |
| **Update Logic** | ~2,000 | Mod updates | **KEEP**: Business logic unchanged |
| **Cloud Integration** | ~1,500 | Firebase/cloud sync | **KEEP**: No UI-specific code |
| **Backup/Restore** | ~800 | Data backup | **KEEP**: Business logic unchanged |
| **Helper Methods** | ~500 | Utilities | **KEEP**: Most unchanged |

---

### 3.2 Conversion Strategies by Section

#### **Strategy 1: Direct Conversion (Low Effort)**

**What**: Code that has direct Avalonia equivalents

**Sections**:
- Private fields (state variables)
- Business logic (preset loading, mod updates, cloud sync)
- Helper methods (path validation, version comparison)
- Constants and configuration

**Approach**:
- Copy code as-is
- Update namespace references (e.g., `System.Windows` → `Avalonia`)
- Test functionality

**Estimated Effort**: ~20% of code-behind (~2,800 lines)

---

#### **Strategy 2: Namespace Updates (Medium Effort)**

**What**: Code that uses WPF types but has Avalonia equivalents

**Sections**:
- Using statements
- Control references (Button, TextBlock, ComboBox)
- Event handler signatures
- Collection view management

**Approach**:
```csharp
// WPF
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

// Avalonia
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
```

**Estimated Effort**: ~15% of code-behind (~2,100 lines)

---

#### **Strategy 3: API Replacement (High Effort)**

**What**: Code that uses WPF-specific APIs without direct Avalonia equivalents

**Sections**:
- Dependency Properties → Avalonia StyledProperty or regular properties
- Visual tree navigation → Avalonia visual tree methods
- File dialogs → Avalonia file dialogs
- Drag-and-drop → Avalonia drag/drop APIs
- Canvas positioning → Avalonia layout system

**Example - Dependency Properties**:

```csharp
// WPF
public static readonly DependencyProperty IsDataBackupInProgressProperty =
    DependencyProperty.Register(
        nameof(IsDataBackupInProgress),
        typeof(bool),
        typeof(MainWindow),
        new PropertyMetadata(false));

public bool IsDataBackupInProgress
{
    get => (bool)GetValue(IsDataBackupInProgressProperty);
    set => SetValue(IsDataBackupInProgressProperty, value);
}

// Avalonia Option 1: StyledProperty (if needed for styling)
public static readonly StyledProperty<bool> IsDataBackupInProgressProperty =
    AvaloniaProperty.Register<MainWindow, bool>(
        nameof(IsDataBackupInProgress),
        defaultValue: false);

public bool IsDataBackupInProgress
{
    get => GetValue(IsDataBackupInProgressProperty);
    set => SetValue(IsDataBackupInProgressProperty, value);
}

// Avalonia Option 2: Regular property with INotifyPropertyChanged
private bool _isDataBackupInProgress;
public bool IsDataBackupInProgress
{
    get => _isDataBackupInProgress;
    set
    {
        if (_isDataBackupInProgress != value)
        {
            _isDataBackupInProgress = value;
            OnPropertyChanged();
        }
    }
}
```

**Example - File Dialogs**:

```csharp
// WPF
var dialog = new OpenFileDialog
{
    Filter = "Mod files (*.zip;*.cs)|*.zip;*.cs",
    Title = "Select a mod file"
};
if (dialog.ShowDialog() == true)
{
    var filePath = dialog.FileName;
}

// Avalonia
var dialog = new OpenFileDialog
{
    Filters = new List<FileDialogFilter>
    {
        new() { Name = "Mod files", Extensions = { "zip", "cs" } }
    },
    Title = "Select a mod file"
};
var result = await dialog.ShowAsync(this);
if (result != null && result.Length > 0)
{
    var filePath = result[0];
}
```

**Estimated Effort**: ~30% of code-behind (~4,200 lines)

---

#### **Strategy 4: Refactor to MVVM (Highest Effort)**

**What**: Code that should move to ViewModel for proper MVVM architecture

**Sections**:
- Menu event handlers → Commands in ViewModel
- Filter logic → ViewModel properties and commands
- Sorting logic → ViewModel collection views
- Selection management → ViewModel selected item properties

**Example - Menu Handlers**:

```csharp
// WPF (Code-behind event handler)
private void SavePresetMenuItem_OnClick(object sender, RoutedEventArgs e)
{
    // Save preset logic...
}

// Avalonia (Command in ViewModel)
// MainViewModel.cs
[RelayCommand]
private async Task SavePresetAsync()
{
    // Save preset logic...
}

// MainWindow.axaml
<MenuItem Header="Save Preset" Command="{Binding SavePresetCommand}" />
```

**Benefits**:
- Better testability
- Cleaner separation of concerns
- Easier to maintain
- More idiomatic Avalonia code

**Estimated Effort**: ~35% of code-behind (~5,000 lines)

---

### 3.3 What Can Stay Unchanged

The following code can **stay mostly unchanged** during conversion:

1. **Business Logic**:
   - Mod installation logic
   - Update checking
   - Dependency resolution
   - Cloud synchronization
   - Backup/restore operations
   - **Lines**: ~5,000 (35%)

2. **Data Structures**:
   - Internal classes (PresetLoadOptions, ModlistMetadata, etc.)
   - Enums (ModlistLoadMode, InstalledModsColumn, etc.)
   - Records (PresetModInstallResult, ModUpdateOperationResult, etc.)
   - **Lines**: ~500 (3.5%)

3. **Validation and Utilities**:
   - Path validation
   - Version comparison
   - String utilities
   - File system operations
   - **Lines**: ~800 (5.6%)

**Total Unchanged**: ~6,300 lines (44%)

---

### 3.4 What Needs Conversion

The following code **requires conversion**:

1. **UI Interaction** (~3,500 lines - 24.6%):
   - Event handlers
   - Visual tree navigation
   - Control manipulation
   - Window lifecycle

2. **WPF-Specific APIs** (~2,500 lines - 17.6%):
   - Dependency properties
   - Drag-and-drop
   - File dialogs
   - Canvas positioning

3. **Should Be Refactored** (~2,000 lines - 14%):
   - Menu handlers → ViewModel commands
   - Filter logic → ViewModel
   - Selection logic → ViewModel

**Total Requiring Conversion**: ~8,000 lines (56%)

---

## Part 4: Conversion Roadmap

### 4.1 Timeline Estimate

| Phase | Duration | Deliverable |
|-------|----------|-------------|
| **Pre-Conversion Refactoring** | 4 weeks | Extracted components in WPF |
| **Phase 1: Foundation** | 2 weeks | Avalonia project setup |
| **Phase 2: Simple Components** | 2 weeks | Converted basic components |
| **Phase 3: Complex Components** | 3 weeks | Converted menu and mod panel |
| **Phase 4: Tab Content** | 3 weeks | Converted all tabs |
| **Phase 5: MainWindow Assembly** | 2 weeks | Integrated MainWindow |
| **Phase 6: Platform Features** | 2 weeks | Cross-platform support |
| **Testing & Refinement** | 2 weeks | Bug fixes and polish |
| **Total** | **20 weeks** (~5 months) | Full Avalonia application |

---

### 4.2 Risk Mitigation

1. **DataGrid Complexity**:
   - **Risk**: Avalonia DataGrid may not support all WPF features
   - **Mitigation**: Prototype early, plan workarounds

2. **ModernWpfUI Dependency**:
   - **Risk**: No direct Avalonia equivalent
   - **Mitigation**: Recreate styles in Avalonia theme

3. **Drag-and-Drop**:
   - **Risk**: Different API may require significant rework
   - **Mitigation**: Prototype drag logic early

4. **Testing Complexity**:
   - **Risk**: Large surface area to test
   - **Mitigation**: Test each component in isolation first

---

### 4.3 Success Criteria

- [ ] All UI components converted to Avalonia
- [ ] Feature parity with WPF version
- [ ] All tests passing
- [ ] Performance equal or better than WPF
- [ ] Cross-platform support (Windows, Linux, macOS)
- [ ] User settings and data migration working
- [ ] No regressions in functionality

---

## Part 5: Recommended Next Steps

### Immediate Actions (Week 1)

1. **Create a feature branch** for pre-conversion refactoring
2. **Extract the Menu System** as the first component
3. **Set up a prototype Avalonia project** to test feasibility
4. **Document current theme/styling** for recreation in Avalonia

### Short-Term Goals (Weeks 2-4)

1. Complete all component extractions
2. Validate WPF version still works after refactoring
3. Build confidence with Avalonia by converting simple components
4. Identify any blockers or challenges early

### Medium-Term Goals (Weeks 5-12)

1. Convert all extracted components to Avalonia
2. Migrate ViewModels and business logic
3. Assemble and integrate MainWindow
4. Comprehensive testing

### Long-Term Goals (Weeks 13-20)

1. Platform-specific testing and optimization
2. User acceptance testing
3. Documentation updates
4. Release planning

---

## Part 6: Additional Recommendations

### Use Reactive Extensions (Rx)

Avalonia works very well with ReactiveUI. Consider migrating from CommunityToolkit.Mvvm to ReactiveUI for better Avalonia integration:

```csharp
// CommunityToolkit.Mvvm (current)
[ObservableProperty]
private string _searchText;

// ReactiveUI (recommended for Avalonia)
private string _searchText;
public string SearchText
{
    get => _searchText;
    set => this.RaiseAndSetIfChanged(ref _searchText, value);
}
```

### Leverage Avalonia DevTools

Use Avalonia DevTools during development for visual tree inspection and style debugging.

### Consider AXAML Previewer

Use the Avalonia XAML Previewer in Visual Studio or Rider for faster UI iteration.

### Plan for Cross-Platform

If cross-platform support is a goal, test on Linux/macOS early and often to catch platform-specific issues.

---

## Conclusion

Converting the MainWindow from WPF to Avalonia is a significant undertaking, but with proper **pre-conversion refactoring** (splitting into components), a **phased conversion approach** (simple → complex), and a **clear code-behind migration strategy** (keep business logic, convert UI logic, refactor to MVVM), the conversion is achievable in approximately **5 months**.

The key to success is:
1. **Extract components first** to reduce complexity
2. **Convert in phases** from simple to complex
3. **Test continuously** to catch issues early
4. **Refactor to proper MVVM** for long-term maintainability

This report provides the roadmap. The next step is to begin pre-conversion refactoring by extracting the Menu System.

---

**Document Version**: 1.0  
**Last Updated**: 2026-01-01  
**Author**: GitHub Copilot Analysis  
**Project**: Simple VS Manager (SVSM)
