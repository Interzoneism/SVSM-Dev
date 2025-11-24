# WPF to Avalonia + SukiUI Migration Progress

## Overview
Migrating the Vintage Story Mod Manager from WPF + ModernWpfUI to Avalonia + SukiUI for cross-platform support.

## Current State Analysis
- **Current Framework**: WPF (net8.0-windows)
- **Target Framework**: Avalonia + SukiUI (cross-platform)
- **MainWindow XAML**: 4,175 lines
- **MainWindow.xaml.cs**: 13,251 lines (very large code-behind)
- **Total XAML views**: 26 files
- **Current UI Library**: ModernWpfUI
- **MVVM Framework**: CommunityToolkit.Mvvm (compatible with Avalonia)

## Migration Strategy

### Phase 1: Project Setup and Infrastructure
**Status**: Not Started

Tasks:
- [ ] Add Avalonia NuGet packages (Avalonia, Avalonia.Desktop, etc.)
- [ ] Add SukiUI NuGet package
- [ ] Update .csproj to support both Windows and cross-platform targets
- [ ] Create App.axaml (Avalonia application)
- [ ] Set up Avalonia application lifecycle
- [ ] Configure desktop builder and startup

### Phase 2: MainWindow Migration (Priority - Largest File)
**Status**: Planning

The MainWindow is extremely large and needs to be broken down:

#### 2.1 Window Structure and Resources
- [ ] Create MainWindow.axaml skeleton
- [ ] Convert Window root element to Avalonia.Controls.Window
- [ ] Migrate Window properties (Title, Size, MinSize, etc.)
- [ ] Convert resource dictionaries to Avalonia format
- [ ] Migrate converters (BooleanToVisibilityConverter, etc.)
- [ ] Set up basic styles

#### 2.2 Layout Structure
- [ ] Analyze and document current Grid structure
- [ ] Convert main Grid layout
- [ ] Convert nested panels and containers
- [ ] Migrate DockPanel, StackPanel elements
- [ ] Set up responsive layout

#### 2.3 Core UI Controls (Section by Section)
- [ ] **Toolbar/Menu Area**: Convert buttons, menus
- [ ] **Mod List Area**: Convert ListView/DataGrid
- [ ] **Search/Filter Area**: Convert TextBox, ComboBox
- [ ] **Mod Details Panel**: Convert content display
- [ ] **Status Bar**: Convert status indicators
- [ ] **Mod Database Tab**: Convert TabControl and content

#### 2.4 Custom Controls
- [ ] OverlappingTagPanel → Avalonia custom panel
- [ ] Custom toggle buttons
- [ ] Custom scrollbars
- [ ] Any other custom WPF controls

#### 2.5 Data Binding and Commands
- [ ] Convert WPF binding syntax to Avalonia
- [ ] Update command bindings
- [ ] Migrate event handlers to MVVM commands where possible
- [ ] Test two-way binding scenarios

### Phase 3: MainWindow Code-Behind Refactoring
**Status**: Not Started

The code-behind is 13K+ lines and needs significant work:

#### 3.1 Business Logic Extraction
- [ ] Identify business logic in code-behind
- [ ] Move logic to ViewModels
- [ ] Move logic to Services
- [ ] Keep only view-specific code in code-behind

#### 3.2 Platform-Specific Code Conversion
- [ ] **Drag & Drop**: Convert WPF DragDrop to Avalonia
- [ ] **File Dialogs**: Convert WPF dialogs to Avalonia StorageProvider
- [ ] **Message Boxes**: Update ModManagerMessageBox for Avalonia
- [ ] **Clipboard**: Convert WPF clipboard to Avalonia
- [ ] **Window Positioning**: Convert WPF window management

#### 3.3 Event Handler Migration
- [ ] Convert routed events to Avalonia events
- [ ] Update event signatures
- [ ] Test event handling
- [ ] Convert attached behaviors

### Phase 4: Supporting Views and Windows
**Status**: Not Started

- [ ] BulkCompatibilityPromptWindow.xaml
- [ ] BulkUpdateChangelogWindow.xaml
- [ ] ModConfigEditorWindow.xaml
- [ ] Dialogs folder views
- [ ] Custom controls (OverlappingTagPanel.cs)

### Phase 5: Theming and Styling
**Status**: Not Started

- [ ] Migrate DarkVsTheme.xaml to Avalonia
- [ ] Convert WPF Brushes to Avalonia Brushes
- [ ] Integrate SukiUI theme system
- [ ] Convert ModernWpf styles to SukiUI equivalents
- [ ] Update color resources
- [ ] Migrate control templates
- [ ] Test theme switching

### Phase 6: Converters and Value Conversion
**Status**: Not Started

Existing converters that need migration:
- [ ] BooleanToVisibilityConverter
- [ ] RowPaddingOffsetConverter
- [ ] DownloadMetricDisplayConverter
- [ ] ScaledThicknessConverter
- [ ] ScaledDoubleConverter
- [ ] DoubleOffsetConverter

### Phase 7: Services and Infrastructure
**Status**: Not Started

- [ ] Update ModManagerMessageBox service
- [ ] File system service (cross-platform paths)
- [ ] Dialog service abstraction
- [ ] Update UserConfigurationService
- [ ] Platform detection and feature flags

### Phase 8: Testing and Validation
**Status**: Not Started

- [ ] Build on Windows
- [ ] Test basic UI rendering
- [ ] Test mod list operations
- [ ] Test mod database
- [ ] Test file operations
- [ ] Build on Linux (if target)
- [ ] Build on macOS (if target)

### Phase 9: Cleanup and Optimization
**Status**: Not Started

- [ ] Remove WPF references
- [ ] Remove ModernWpfUI package
- [ ] Update documentation
- [ ] Performance optimization
- [ ] Code cleanup

## Key WPF → Avalonia Conversion Notes

### Namespace Changes
- `System.Windows` → `Avalonia`
- `System.Windows.Controls` → `Avalonia.Controls`
- `System.Windows.Data` → `Avalonia.Data`
- `System.Windows.Media` → `Avalonia.Media`

### XAML Differences
- `xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"` → `xmlns="https://github.com/avaloniaui"`
- `Window.Title` → `Title` (property stays same)
- `DependencyProperty` → `StyledProperty` / `DirectProperty`
- `RoutedEvent` → Avalonia events
- `ICommand` works the same (CommunityToolkit.Mvvm compatible)

### Control Equivalents
- `Window` → `Window` (similar)
- `ListView` → `ListBox` or `ItemsRepeater`
- `DataGrid` → `DataGrid` (Avalonia has one)
- `TextBox` → `TextBox` (similar)
- `ComboBox` → `ComboBox` (similar)
- `TabControl` → `TabControl` (similar)
- ModernWpf controls → SukiUI equivalents

### Not Directly Supported (Need Workarounds)
- `VisualTreeHelper` → different API in Avalonia
- `DependencyObject` → `AvaloniaObject`
- Some attached properties work differently
- Triggers → Use data binding and converters instead

## Session Log

### Session 1 (Current)
**Date**: 2025-11-24
**Agent**: Initial assessment and planning

**Completed**:
- ✅ Analyzed existing WPF project structure
- ✅ Documented MainWindow size and complexity
- ✅ Created migration strategy
- ✅ Created this MIGRATION_PROGRESS.md file
- ✅ Built current WPF project to verify working state

**Observations**:
- MainWindow is extremely large (4,175 XAML + 13,251 C# lines)
- Heavy use of ModernWpfUI custom controls
- Extensive custom styling in App.xaml
- Large code-behind suggests significant refactoring needed
- CommunityToolkit.Mvvm should work with Avalonia (good news!)

**Next Steps**:
1. Add Avalonia NuGet packages to project
2. Create basic App.axaml with SukiUI
3. Create MainWindow.axaml skeleton
4. Convert first section of MainWindow (e.g., basic window structure)
5. Test that Avalonia application launches

**Recommendations**:
- Start small, test often
- Convert MainWindow in sections, not all at once
- Keep WPF version in git history but commit to Avalonia
- May need to create helper utilities for common conversions
- Consider using Avalonia.Markup.Xaml.PortableXaml for complex bindings

---

## For Next Agent

### Where We Left Off
We have completed the initial assessment and planning. The project currently builds successfully as a WPF application. A comprehensive migration plan has been created.

### What To Do Next
1. **Add Avalonia packages** to VintageStoryModManager.csproj
2. **Create App.axaml** - Start with a basic Avalonia application
3. **Create MainWindow.axaml skeleton** - Just the Window element and basic structure
4. **Update Program.cs** or create one to bootstrap Avalonia
5. **Test that it builds** - Don't worry about functionality yet, just get it to compile

### Key Files to Modify
- `VintageStoryModManager.csproj` - Add Avalonia packages, update target framework
- Create `App.axaml` and `App.axaml.cs` (can coexist with WPF initially)
- Create `MainWindow.axaml` and `MainWindow.axaml.cs` (Avalonia versions)
- May need `Program.cs` for Avalonia desktop entry point

### Packages to Add
```xml
<PackageReference Include="Avalonia" Version="11.0.+" />
<PackageReference Include="Avalonia.Desktop" Version="11.0.+" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="11.0.+" />
<PackageReference Include="SukiUI" Version="5.+" />
```

### Important Notes
- Don't delete WPF files yet - we may need to reference them
- Start with minimal Avalonia app that just opens a window
- Test build frequently
- Document any issues or discoveries in this file

Good luck! 🚀
